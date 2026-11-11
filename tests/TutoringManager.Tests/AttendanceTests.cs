using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TutoringManager.Api.Attendance;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class AttendanceTests(ApiFactory factory)
{
    [Fact]
    public async Task Student_confirms_through_the_link_and_the_tutor_sees_it()
    {
        var tutor = await factory.CreateTutorClientAsync("Maria Tutor");
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-20", "17:00");
        var token = await CreateTokenAsync(lesson.Id);
        var studentBrowser = factory.CreateClient();

        var page = await (await studentBrowser.GetAsync($"/api/attendance/{token}")).ReadAsync<AttendancePageResponse>();
        Assert.Equal("Maria Tutor", page.TutorName);
        Assert.Equal(new TimeOnly(17, 0), page.StartTime);
        Assert.Equal(AttendanceResponse.Unknown, page.Response);

        var answered = await (await Respond(studentBrowser, token, "Confirmed")).ReadAsync<AttendancePageResponse>();
        Assert.Equal(AttendanceResponse.Confirmed, answered.Response);
        Assert.Equal(AttendanceResponse.Confirmed, (await LessonTests.GetLessonAsync(tutor, lesson.Id)).Attendance);
    }

    [Fact]
    public async Task Student_can_change_the_answer_before_the_lesson()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-21", "17:00");
        var token = await CreateTokenAsync(lesson.Id);
        var studentBrowser = factory.CreateClient();

        await Respond(studentBrowser, token, "Confirmed");
        await Respond(studentBrowser, token, "Declined");

        Assert.Equal(AttendanceResponse.Declined, (await LessonTests.GetLessonAsync(tutor, lesson.Id)).Attendance);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // well-formed, but never issued
    public async Task Unknown_token_gets_404(string token)
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/attendance/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Respond(client, token, "Confirmed")).StatusCode);
    }

    [Fact]
    public async Task Token_expires_when_the_lesson_starts()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        // 09:30 Warsaw = 08:30 UTC, already started at the fake "now" of 09:00 UTC.
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-11", "09:30");
        var token = await CreateTokenAsync(lesson.Id);

        var response = await Respond(factory.CreateClient(), token, "Confirmed");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Moving_the_lesson_revokes_the_link()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-22", "17:00");
        var token = await CreateTokenAsync(lesson.Id);
        await Respond(factory.CreateClient(), token, "Confirmed");

        await tutor.PutAsJsonAsync($"/api/lessons/{lesson.Id}", new { date = "2027-01-23", startTime = "10:00", durationMinutes = 60 });

        Assert.Equal(HttpStatusCode.NotFound, (await Respond(factory.CreateClient(), token, "Confirmed")).StatusCode);
        Assert.Equal(AttendanceResponse.Unknown, (await LessonTests.GetLessonAsync(tutor, lesson.Id)).Attendance);
    }

    [Fact]
    public async Task Changing_only_the_duration_keeps_the_link_valid()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-26", "17:00");
        var token = await CreateTokenAsync(lesson.Id);

        await tutor.PutAsJsonAsync($"/api/lessons/{lesson.Id}", new { date = "2027-01-26", startTime = "17:00", durationMinutes = 90 });

        Assert.Equal(HttpStatusCode.OK, (await Respond(factory.CreateClient(), token, "Confirmed")).StatusCode);
    }

    [Fact]
    public async Task Cancelling_the_lesson_revokes_the_link()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-27", "17:00");
        var token = await CreateTokenAsync(lesson.Id);

        await tutor.PostAsync($"/api/lessons/{lesson.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/attendance/{token}")).StatusCode);
    }

    [Fact]
    public async Task A_token_only_answers_for_its_own_lesson()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var mine = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-28", "10:00");
        var other = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-28", "12:00");
        var token = await CreateTokenAsync(mine.Id);

        await Respond(factory.CreateClient(), token, "Declined");

        Assert.Equal(AttendanceResponse.Declined, (await LessonTests.GetLessonAsync(tutor, mine.Id)).Attendance);
        Assert.Equal(AttendanceResponse.Unknown, (await LessonTests.GetLessonAsync(tutor, other.Id)).Attendance);
    }

    [Fact]
    public async Task Answer_must_be_confirmed_or_declined()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-29", "10:00");
        var token = await CreateTokenAsync(lesson.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await Respond(factory.CreateClient(), token, "Unknown")).StatusCode);
    }

    [Fact]
    public async Task Attendance_endpoints_are_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:AttendancePerMinute", "3"));
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.GetAsync("/api/attendance/not-a-token")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Only_the_hash_of_the_token_is_stored()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(tutor);
        var lesson = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-30", "10:00");
        var token = await CreateTokenAsync(lesson.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .AttendanceTokens.SingleAsync(t => t.LessonId == lesson.Id);
        Assert.Equal(32, stored.TokenHash.Length);
        Assert.Equal(AttendanceToken.HashOf(token), stored.TokenHash);
        Assert.DoesNotContain(token, Convert.ToBase64String(stored.TokenHash));
    }

    public static Task<HttpResponseMessage> Respond(HttpClient client, string token, string response) =>
        client.PostAsJsonAsync($"/api/attendance/{token}", new { response });

    private async Task<string> CreateTokenAsync(Guid lessonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await db.Lessons.IgnoreQueryFilters().SingleAsync(l => l.Id == lessonId);
        return await scope.ServiceProvider.GetRequiredService<AttendanceService>().CreateTokenAsync(lesson);
    }
}
