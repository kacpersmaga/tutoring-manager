using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TutoringManager.Api.Lessons;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class SeriesTests(ApiFactory factory)
{
    // The fake clock starts at Monday 2027-01-11 09:00 UTC; 12 weeks ahead is 2027-04-05.

    [Fact]
    public async Task Series_generates_weekly_lessons_twelve_weeks_ahead()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client, rate: 90m);

        var series = await CreateSeriesAsync(client, student.Id, "2027-01-12", "17:00"); // Tuesdays

        var lessons = await GetSeriesLessonsAsync(client, series.Id);
        Assert.Equal(12, lessons.Count); // 12 Jan .. 30 Mar
        Assert.All(lessons, l => Assert.Equal(DayOfWeek.Tuesday, l.Date.DayOfWeek));
        Assert.All(lessons, l => Assert.Equal(90m, l.PricePerLesson));
    }

    [Fact]
    public async Task Series_keeps_the_local_hour_across_the_spring_daylight_saving_change()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);

        var series = await CreateSeriesAsync(client, student.Id, "2027-03-15", "17:00"); // Mondays; DST starts 28 March 2027

        var lessons = await GetSeriesLessonsAsync(client, series.Id);
        var beforeChange = lessons.Single(l => l.Date == new DateOnly(2027, 3, 22));
        var afterChange = lessons.Single(l => l.Date == new DateOnly(2027, 3, 29));
        Assert.Equal(new TimeOnly(17, 0), beforeChange.StartTime);
        Assert.Equal(new TimeOnly(17, 0), afterChange.StartTime);
        Assert.Equal(16, beforeChange.StartUtc.Hour);
        Assert.Equal(15, afterChange.StartUtc.Hour);
    }

    [Fact]
    public async Task Series_that_would_overlap_an_existing_lesson_is_rejected_as_a_whole()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        await LessonTests.CreateLessonAsync(client, student.Id, "2027-02-02", "17:30");

        var response = await client.PostAsJsonAsync("/api/series",
            new { studentId = student.Id, startDate = "2027-01-12", startTime = "17:00", durationMinutes = 60 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await (await client.GetAsync("/api/series")).ReadAsync<List<SeriesResponse>>());
    }

    [Fact]
    public async Task Editing_the_series_changes_future_lessons_but_not_exceptions_or_the_past()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var series = await CreateSeriesAsync(client, student.Id, "2027-01-04", "17:00", endDate: "2027-02-22"); // Mondays
        var lessons = await GetSeriesLessonsAsync(client, series.Id);

        var past = lessons.Single(l => l.Date == new DateOnly(2027, 1, 4));
        var moved = lessons.Single(l => l.Date == new DateOnly(2027, 2, 1));
        await client.PutAsJsonAsync($"/api/lessons/{moved.Id}", new { date = "2027-02-02", startTime = "16:00", durationMinutes = 60 });
        var cancelled = lessons.Single(l => l.Date == new DateOnly(2027, 2, 8));
        await client.PostAsync($"/api/lessons/{cancelled.Id}/cancel", null);

        var response = await client.PutAsJsonAsync($"/api/series/{series.Id}",
            new { dayOfWeek = "Monday", startTime = "18:30", durationMinutes = 45, endDate = "2027-02-22" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await GetSeriesLessonsAsync(client, series.Id);
        Assert.Equal(new TimeOnly(17, 0), after.Single(l => l.Id == past.Id).StartTime);
        Assert.Equal(new DateOnly(2027, 2, 2), after.Single(l => l.Id == moved.Id).Date);
        Assert.Equal(LessonStatus.Cancelled, after.Single(l => l.Id == cancelled.Id).Status);
        Assert.DoesNotContain(after, l => l.Date == new DateOnly(2027, 2, 1)); // no duplicate for the moved week

        // 11 Jan (later today), 18 Jan, 25 Jan, 15 Feb, 22 Feb
        var regenerated = after.Where(l => !l.IsModifiedFromSeries && l.Date >= new DateOnly(2027, 1, 11)).ToList();
        Assert.Equal(5, regenerated.Count);
        Assert.All(regenerated, l => Assert.Equal(new TimeOnly(18, 30), l.StartTime));
        Assert.All(regenerated, l => Assert.Equal(45, l.DurationMinutes));
    }

    [Fact]
    public async Task Deleting_the_series_keeps_past_lessons_and_exceptions()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var series = await CreateSeriesAsync(client, student.Id, "2027-01-04", "17:00", endDate: "2027-02-22");
        var lessons = await GetSeriesLessonsAsync(client, series.Id);
        var past = lessons.Single(l => l.Date == new DateOnly(2027, 1, 4));
        var withNote = lessons.Single(l => l.Date == new DateOnly(2027, 2, 15));
        await client.PutAsJsonAsync($"/api/lessons/{withNote.Id}/note", new { content = "Prepare for the test" });

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/series/{series.Id}")).StatusCode);

        var remaining = await (await client.GetAsync("/api/lessons?from=2027-01-01&to=2027-02-28")).ReadAsync<List<LessonResponse>>();
        Assert.Equal([past.Id, withNote.Id], remaining.Select(l => l.Id));
        Assert.All(remaining, l => Assert.Null(l.SeriesId));
    }

    [Fact]
    public async Task Daily_extension_tops_the_series_up_without_duplicates()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var series = await CreateSeriesAsync(client, student.Id, "2027-01-13", "08:00"); // Wednesdays
        var before = await GetSeriesLessonsAsync(client, series.Id, to: "2027-06-30");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<SeriesService>();
            await service.ExtendAllAsync(new DateOnly(2027, 2, 10)); // four weeks later
            await service.ExtendAllAsync(new DateOnly(2027, 2, 10)); // running twice changes nothing
        }

        var after = await GetSeriesLessonsAsync(client, series.Id, to: "2027-06-30");
        Assert.Equal(before.Count + 5, after.Count); // 7, 14, 21, 28 April and 5 May
        Assert.Equal(after.Count, after.Select(l => l.Date).Distinct().Count());
        Assert.Equal(new DateOnly(2027, 5, 5), after.Max(l => l.Date));
    }

    [Fact]
    public async Task Tutor_cannot_change_another_tutors_series()
    {
        var owner = await factory.CreateTutorClientAsync();
        var intruder = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(owner);
        var series = await CreateSeriesAsync(owner, student.Id, "2027-01-14", "12:00");

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/series/{series.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/series/{series.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await intruder.PostAsJsonAsync("/api/series",
            new { studentId = student.Id, startDate = "2027-01-14", startTime = "12:00", durationMinutes = 60 })).StatusCode);
    }

    private static async Task<SeriesResponse> CreateSeriesAsync(
        HttpClient client, Guid studentId, string startDate, string startTime, string? endDate = null) =>
        await (await client.PostAsJsonAsync("/api/series",
            new { studentId, startDate, startTime, durationMinutes = 60, endDate })).ReadAsync<SeriesResponse>();

    private static async Task<List<LessonResponse>> GetSeriesLessonsAsync(
        HttpClient client, Guid seriesId, string from = "2027-01-01", string to = "2027-04-30")
    {
        var lessons = new List<LessonResponse>();
        for (var start = DateOnly.Parse(from); start <= DateOnly.Parse(to); start = start.AddDays(60))
        {
            var end = start.AddDays(59) < DateOnly.Parse(to) ? start.AddDays(59) : DateOnly.Parse(to);
            lessons.AddRange(await (await client.GetAsync($"/api/lessons?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}"))
                .ReadAsync<List<LessonResponse>>());
        }

        return lessons.Where(l => l.SeriesId == seriesId).OrderBy(l => l.StartUtc).ToList();
    }
}
