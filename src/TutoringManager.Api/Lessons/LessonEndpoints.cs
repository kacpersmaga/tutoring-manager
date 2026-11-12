namespace TutoringManager.Api.Lessons;

public static class LessonEndpoints
{
    public static void MapLessonEndpoints(this IEndpointRouteBuilder app)
    {
        var lessons = app.MapGroup("/api/lessons").RequireAuthorization();

        lessons.MapGet("/", (DateOnly from, DateOnly to, LessonService service) => service.GetCalendarAsync(from, to));
        lessons.MapGet("/{id:guid}", (Guid id, LessonService service) => service.GetAsync(id));
        lessons.MapPost("/", async (CreateLessonRequest request, LessonService service) =>
        {
            var lesson = await service.CreateAsync(request);
            return Results.Created($"/api/lessons/{lesson.Id}", lesson);
        });
        lessons.MapPut("/{id:guid}", (Guid id, UpdateLessonRequest request, LessonService service) => service.UpdateAsync(id, request));
        lessons.MapPost("/{id:guid}/cancel", (Guid id, LessonService service) => service.CancelAsync(id));
        lessons.MapPost("/{id:guid}/complete", (Guid id, LessonService service) => service.CompleteAsync(id));
        lessons.MapPost("/complete-past", (LessonService service) => service.CompletePastAsync());
        lessons.MapPut("/{id:guid}/note", (Guid id, SaveNoteRequest request, LessonService service) => service.SaveNoteAsync(id, request));
        lessons.MapDelete("/{id:guid}/note", async (Guid id, LessonService service) =>
        {
            await service.DeleteNoteAsync(id);
            return Results.NoContent();
        });

        var series = app.MapGroup("/api/series").RequireAuthorization();

        series.MapGet("/", (Guid? studentId, SeriesService service) => service.ListAsync(studentId));
        series.MapGet("/{id:guid}", (Guid id, SeriesService service) => service.GetAsync(id));
        series.MapPost("/", async (CreateSeriesRequest request, SeriesService service) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/series/{created.Id}", created);
        });
        series.MapPut("/{id:guid}", (Guid id, UpdateSeriesRequest request, SeriesService service) => service.UpdateAsync(id, request));
        series.MapDelete("/{id:guid}", async (Guid id, SeriesService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        });

        app.MapGet("/api/students/{id:guid}/notes", (Guid id, int? skip, LessonService service) => service.GetStudentNotesAsync(id, skip ?? 0))
            .RequireAuthorization();
        app.MapGet("/api/students/{id:guid}/lessons-without-note", (Guid id, LessonService service) => service.GetLessonsWithoutNoteAsync(id))
            .RequireAuthorization();
    }
}
