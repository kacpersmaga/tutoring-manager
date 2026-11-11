using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TutoringManager.Api.Emails;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

/// <summary>
/// Uses its own database and clock (class fixture), because these tests move time forward
/// and process the whole email queue.
/// </summary>
public partial class EmailTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    [Fact]
    public async Task Email_is_sent_24_hours_before_the_lesson_with_a_working_link()
    {
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromDays(3));
        await DrainQueueAsync();
        Assert.Empty(factory.Emails.SentTo(email));

        var queued = Assert.Single(await GetEmailsAsync(lesson.Id));
        Assert.Equal(lesson.StartUtc.AddHours(-24), queued.SendAtUtc);

        factory.Time.SetUtcNow(new DateTimeOffset(queued.SendAtUtc, TimeSpan.Zero));
        await DrainQueueAsync();

        var sent = Assert.Single(factory.Emails.SentTo(email));
        var token = LinkToken().Match(sent.TextBody).Groups[1].Value;
        var page = await factory.CreateClient().GetAsync($"/api/attendance/{token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(EmailStatus.Sent, (await LessonTests.GetLessonAsync(tutor, lesson.Id)).EmailStatus);
    }

    [Fact]
    public async Task Lesson_sooner_than_24_hours_gets_the_email_right_away()
    {
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        await CreateLessonInAsync(tutor, studentId, TimeSpan.FromHours(5));

        await DrainQueueAsync();

        Assert.Single(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task Two_workers_running_in_parallel_send_each_email_once()
    {
        var addresses = new List<string>();
        for (var t = 0; t < 2; t++)
        {
            var tutor = await factory.CreateTutorClientAsync();
            for (var i = 0; i < 15; i++)
            {
                var address = $"parallel-{Guid.NewGuid():N}@example.com";
                var student = await StudentTests.CreateStudentAsync(tutor, email: address);
                await CreateLessonInAsync(tutor, student.Id, TimeSpan.FromMinutes(61 * (i + 1)));
                addresses.Add(address);
            }
        }

        // 30 due emails, batches of 20: both workers compete for the same rows.
        await Task.WhenAll(DrainQueueAsync(), DrainQueueAsync());

        Assert.All(addresses, address => Assert.Single(factory.Emails.SentTo(address)));
    }

    [Fact]
    public async Task Email_claimed_by_a_crashed_worker_is_sent_after_the_lease_expires()
    {
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromHours(4));

        // Worker A claims the batch and "crashes" before sending anything.
        await using (var crashed = factory.Services.CreateAsyncScope())
        {
            await crashed.ServiceProvider.GetRequiredService<EmailDispatcher>().ClaimBatchAsync();
        }

        await DrainQueueAsync();
        Assert.Empty(factory.Emails.SentTo(email)); // still leased by A

        factory.Time.Advance(TimeSpan.FromMinutes(6));
        await DrainQueueAsync();

        Assert.Single(factory.Emails.SentTo(email));
        Assert.Equal(EmailStatus.Sent, Assert.Single(await GetEmailsAsync(lesson.Id)).Status);
    }

    [Fact]
    public async Task Provider_error_is_retried_with_backoff()
    {
        await DrainQueueAsync(); // nothing else due, so the simulated failure hits this test's email
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromHours(6));
        factory.Emails.FailNext(1);

        await DrainQueueAsync();

        var afterFailure = Assert.Single(await GetEmailsAsync(lesson.Id));
        Assert.Equal(EmailStatus.Pending, afterFailure.Status);
        Assert.Equal(1, afterFailure.Attempts);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime.AddMinutes(1), afterFailure.NextAttemptAtUtc);
        Assert.Equal("SMTP server unavailable", afterFailure.LastError);

        await DrainQueueAsync();
        Assert.Empty(factory.Emails.SentTo(email)); // not before the backoff

        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await DrainQueueAsync();
        Assert.Single(factory.Emails.SentTo(email));
        Assert.Equal(EmailStatus.Sent, Assert.Single(await GetEmailsAsync(lesson.Id)).Status);
    }

    [Fact]
    public async Task Email_fails_for_good_after_five_attempts_and_the_tutor_sees_it()
    {
        await DrainQueueAsync();
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromHours(20));
        factory.Emails.FailNext(5);

        await DrainQueueAsync();
        foreach (var delay in ScheduledEmail.RetryDelays)
        {
            factory.Time.Advance(delay);
            await DrainQueueAsync();
        }

        var failed = Assert.Single(await GetEmailsAsync(lesson.Id));
        Assert.Equal(EmailStatus.Failed, failed.Status);
        Assert.Equal(5, failed.Attempts);
        Assert.Empty(factory.Emails.SentTo(email));
        Assert.Equal(EmailStatus.Failed, (await LessonTests.GetLessonAsync(tutor, lesson.Id)).EmailStatus);
    }

    [Fact]
    public async Task Cancelling_the_lesson_cancels_its_email()
    {
        var (tutor, studentId, email) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromDays(2));

        await tutor.PostAsync($"/api/lessons/{lesson.Id}/cancel", null);
        factory.Time.Advance(TimeSpan.FromDays(1));
        await DrainQueueAsync();

        Assert.Equal(EmailStatus.Cancelled, Assert.Single(await GetEmailsAsync(lesson.Id)).Status);
        Assert.Empty(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task Moving_the_lesson_reschedules_its_email()
    {
        var (tutor, studentId, _) = await CreateTutorAndStudentAsync();
        var lesson = await CreateLessonInAsync(tutor, studentId, TimeSpan.FromDays(3));
        var (date, time) = LocalSlot(TimeSpan.FromDays(5));

        var moved = await (await tutor.PutAsJsonAsync($"/api/lessons/{lesson.Id}",
            new { date, startTime = time, durationMinutes = 60 })).ReadAsync<LessonResponse>();

        var emails = await GetEmailsAsync(lesson.Id);
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, e => e.Status == EmailStatus.Cancelled && e.SendAtUtc == lesson.StartUtc.AddHours(-24));
        Assert.Contains(emails, e => e.Status == EmailStatus.Pending && e.SendAtUtc == moved.StartUtc.AddHours(-24));
    }

    [Fact]
    public async Task New_series_queues_one_email_per_future_lesson()
    {
        var (tutor, studentId, _) = await CreateTutorAndStudentAsync();
        var (date, time) = LocalSlot(TimeSpan.FromDays(1));

        await tutor.PostAsJsonAsync("/api/series", new { studentId, startDate = date, startTime = time, durationMinutes = 60 });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lessonIds = await db.Lessons.IgnoreQueryFilters().Where(l => l.StudentId == studentId).Select(l => l.Id).ToListAsync();
        var emailCount = await db.ScheduledEmails.IgnoreQueryFilters().CountAsync(e => lessonIds.Contains(e.LessonId!.Value));
        Assert.Equal(12, lessonIds.Count);
        Assert.Equal(12, emailCount);
    }

    private async Task<(HttpClient Tutor, Guid StudentId, string Email)> CreateTutorAndStudentAsync()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var email = $"student-{Guid.NewGuid():N}@example.com";
        var student = await StudentTests.CreateStudentAsync(tutor, email: email);
        return (tutor, student.Id, email);
    }

    private async Task<LessonResponse> CreateLessonInAsync(HttpClient tutor, Guid studentId, TimeSpan fromNow)
    {
        var (date, time) = LocalSlot(fromNow);
        return await LessonTests.CreateLessonAsync(tutor, studentId, date, time);
    }

    private (string Date, string Time) LocalSlot(TimeSpan fromNow)
    {
        var local = TimeZoneInfo.ConvertTime(factory.Time.GetUtcNow() + fromNow, Warsaw);
        return (local.ToString("yyyy-MM-dd"), local.ToString("HH:mm"));
    }

    private async Task DrainQueueAsync()
    {
        int claimed;
        do
        {
            await using var scope = factory.Services.CreateAsyncScope();
            claimed = await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().ProcessBatchAsync();
        }
        while (claimed > 0);
    }

    private async Task<List<ScheduledEmail>> GetEmailsAsync(Guid lessonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ScheduledEmails
            .IgnoreQueryFilters().AsNoTracking().Where(e => e.LessonId == lessonId).ToListAsync();
    }

    [GeneratedRegex("/a/([A-Za-z0-9_-]{43})")]
    private static partial Regex LinkToken();
}
