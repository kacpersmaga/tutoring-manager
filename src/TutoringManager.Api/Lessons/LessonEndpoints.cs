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

        app.MapGet("/api/students/{id:guid}/notes", (Guid id, LessonService service) => service.GetStudentNotesAsync(id))
            .RequireAuthorization();
    }
}
