using System.Net;
using System.Net.Http.Json;
using TutoringManager.Api.Lessons;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class LessonTests(ApiFactory factory)
{
    // The fake clock starts at 2027-01-11 09:00 UTC (a Monday).

    [Fact]
    public async Task Lesson_stores_local_time_and_computes_utc()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client, rate: 150m);

        var lesson = await CreateLessonAsync(client, student.Id, "2027-01-20", "17:00");

        Assert.Equal(new TimeOnly(17, 0), lesson.StartTime);
        Assert.Equal(new DateTime(2027, 1, 20, 16, 0, 0), lesson.StartUtc); // Warsaw is UTC+1 in winter
        Assert.Equal(150m, lesson.PricePerLesson);
        Assert.Equal(LessonStatus.Scheduled, lesson.Status);
    }

    [Fact]
    public async Task Overlapping_lesson_is_rejected_with_409()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        await CreateLessonAsync(client, student.Id, "2027-01-20", "17:00", 60);

        var overlapping = await PostLessonAsync(client, student.Id, "2027-01-20", "17:30", 60);
        var backToBack = await PostLessonAsync(client, student.Id, "2027-01-20", "18:00", 60);

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Equal(HttpStatusCode.Created, backToBack.StatusCode);
    }

    [Fact]
    public async Task Two_concurrent_overlapping_requests_create_only_one_lesson()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => PostLessonAsync(client, student.Id, "2027-02-03", "10:00", 60)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Cancelled_lesson_frees_the_slot()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var lesson = await CreateLessonAsync(client, student.Id, "2027-01-21", "09:00");

        await client.PostAsync($"/api/lessons/{lesson.Id}/cancel", null);
        var replacement = await PostLessonAsync(client, student.Id, "2027-01-21", "09:00", 60);

        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
    }

    [Fact]
    public async Task Different_tutors_can_teach_at_the_same_time()
    {
        var tutorA = await factory.CreateTutorClientAsync();
        var tutorB = await factory.CreateTutorClientAsync();
        var studentA = await StudentTests.CreateStudentAsync(tutorA);
        var studentB = await StudentTests.CreateStudentAsync(tutorB);

        Assert.Equal(HttpStatusCode.Created, (await PostLessonAsync(tutorA, studentA.Id, "2027-01-22", "12:00", 60)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostLessonAsync(tutorB, studentB.Id, "2027-01-22", "12:00", 60)).StatusCode);
    }

    [Fact]
    public async Task Moving_a_lesson_onto_another_one_is_rejected()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        await CreateLessonAsync(client, student.Id, "2027-01-25", "10:00");
        var second = await CreateLessonAsync(client, student.Id, "2027-01-25", "12:00");

        var response = await client.PutAsJsonAsync($"/api/lessons/{second.Id}",
            new { date = "2027-01-25", startTime = "10:30", durationMinutes = 60 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Status_transitions_follow_the_rules()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var future = await CreateLessonAsync(client, student.Id, "2027-03-01", "10:00");
        var past = await CreateLessonAsync(client, student.Id, "2027-01-04", "10:00");

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/lessons/{future.Id}/complete", null)).StatusCode);

        var completed = await (await client.PostAsync($"/api/lessons/{past.Id}/complete", null)).ReadAsync<LessonResponse>();
        Assert.Equal(LessonStatus.Completed, completed.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/lessons/{past.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/lessons/{past.Id}",
            new { date = "2027-01-05", startTime = "10:00", durationMinutes = 60 })).StatusCode);

        await client.PostAsync($"/api/lessons/{future.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/lessons/{future.Id}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Complete_past_marks_only_finished_scheduled_lessons()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var past1 = await CreateLessonAsync(client, student.Id, "2027-01-04", "10:00");
        var past2 = await CreateLessonAsync(client, student.Id, "2027-01-05", "10:00");
        var cancelled = await CreateLessonAsync(client, student.Id, "2027-01-06", "10:00");
        await client.PostAsync($"/api/lessons/{cancelled.Id}/cancel", null);
        var future = await CreateLessonAsync(client, student.Id, "2027-01-20", "10:00");

        var result = await (await client.PostAsync("/api/lessons/complete-past", null)).ReadAsync<CompletePastResponse>();

        Assert.Equal(2, result.Completed);
        Assert.Equal(LessonStatus.Cancelled, (await GetLessonAsync(client, cancelled.Id)).Status);
        Assert.Equal(LessonStatus.Scheduled, (await GetLessonAsync(client, future.Id)).Status);
        Assert.Equal(LessonStatus.Completed, (await GetLessonAsync(client, past1.Id)).Status);
        Assert.Equal(LessonStatus.Completed, (await GetLessonAsync(client, past2.Id)).Status);
    }

    [Fact]
    public async Task Calendar_returns_lessons_in_range()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        await CreateLessonAsync(client, student.Id, "2027-01-18", "10:00");
        await CreateLessonAsync(client, student.Id, "2027-01-24", "10:00");
        await CreateLessonAsync(client, student.Id, "2027-01-25", "10:00");

        var week = await (await client.GetAsync("/api/lessons?from=2027-01-18&to=2027-01-24")).ReadAsync<List<LessonResponse>>();

        Assert.Equal(2, week.Count);
        Assert.All(week, l => Assert.Equal(student.FullName, l.StudentName));
    }

    [Fact]
    public async Task Note_can_be_saved_updated_and_listed_for_the_student()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var older = await CreateLessonAsync(client, student.Id, "2027-01-04", "10:00");
        var newer = await CreateLessonAsync(client, student.Id, "2027-01-07", "10:00");

        await client.PutAsJsonAsync($"/api/lessons/{older.Id}/note", new { content = "Quadratic equations." });
        await client.PutAsJsonAsync($"/api/lessons/{newer.Id}/note", new { content = "Draft" });
        var saved = await (await client.PutAsJsonAsync($"/api/lessons/{newer.Id}/note", new { content = "Vieta's formulas. Homework: ex. 4-9." }))
            .ReadAsync<LessonResponse>();
        Assert.Equal("Vieta's formulas. Homework: ex. 4-9.", saved.Note);

        var notes = await (await client.GetAsync($"/api/students/{student.Id}/notes")).ReadAsync<List<StudentNoteResponse>>();
        Assert.Equal([newer.Id, older.Id], notes.Select(n => n.LessonId));
    }

    [Fact]
    public async Task Note_longer_than_4000_characters_is_rejected()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(client);
        var lesson = await CreateLessonAsync(client, student.Id, "2027-01-04", "10:00");

        var response = await client.PutAsJsonAsync($"/api/lessons/{lesson.Id}/note", new { content = new string('a', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tutor_cannot_touch_another_tutors_lesson_or_note()
    {
        var owner = await factory.CreateTutorClientAsync();
        var intruder = await factory.CreateTutorClientAsync();
        var student = await StudentTests.CreateStudentAsync(owner);
        var lesson = await CreateLessonAsync(owner, student.Id, "2027-01-04", "10:00");
        await owner.PutAsJsonAsync($"/api/lessons/{lesson.Id}/note", new { content = "Private" });

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/lessons/{lesson.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync($"/api/lessons/{lesson.Id}/note", new { content = "Mine now" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/lessons/{lesson.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/students/{student.Id}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostLessonAsync(intruder, student.Id, "2027-01-30", "10:00", 60)).StatusCode);
        Assert.Empty(await (await intruder.GetAsync("/api/lessons?from=2027-01-01&to=2027-01-31")).ReadAsync<List<LessonResponse>>());

        Assert.Equal("Private", (await GetLessonAsync(owner, lesson.Id)).Note);
    }

    public static Task<HttpResponseMessage> PostLessonAsync(HttpClient client, Guid studentId, string date, string time, int duration) =>
        client.PostAsJsonAsync("/api/lessons", new { studentId, date, startTime = time, durationMinutes = duration });

    public static async Task<LessonResponse> CreateLessonAsync(HttpClient client, Guid studentId, string date, string time, int duration = 60) =>
        await (await PostLessonAsync(client, studentId, date, time, duration)).ReadAsync<LessonResponse>();

    public static async Task<LessonResponse> GetLessonAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/lessons/{id}")).ReadAsync<LessonResponse>();
}
