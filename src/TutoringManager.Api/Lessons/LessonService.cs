using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;

namespace TutoringManager.Api.Lessons;

public class LessonService(AppDbContext db, CurrentTutor currentTutor, TimeProvider time)
{
    private const int MaxCalendarDays = 62;

    public async Task<List<LessonResponse>> GetCalendarAsync(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxCalendarDays)
        {
            throw ApiException.Validation("to", $"The range must be between 0 and {MaxCalendarDays} days.");
        }

        return await db.Lessons
            .Where(l => l.LocalDate >= from && l.LocalDate <= to)
            .OrderBy(l => l.StartUtc)
            .Select(ToResponse)
            .ToListAsync();
    }

    public async Task<LessonResponse> GetAsync(Guid id) =>
        await db.Lessons.Where(l => l.Id == id).Select(ToResponse).FirstOrDefaultAsync()
        ?? throw ApiException.NotFound("Lesson");

    public async Task<LessonResponse> CreateAsync(CreateLessonRequest request)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId)
            ?? throw ApiException.Validation("studentId", "Student not found.");

        var lesson = new Lesson
        {
            TutorId = currentTutor.RequiredId,
            StudentId = student.Id,
            PricePerLesson = student.RatePerLesson,
        };
        lesson.SetTime(request.Date, request.StartTime, request.DurationMinutes, await GetTutorTimeZoneAsync());

        await EnsureNoOverlapAsync(lesson);
        db.Lessons.Add(lesson);
        await SaveAsync();
        return await GetAsync(lesson.Id);
    }

    /// <summary>Moves a lesson or changes its length. A lesson from a series becomes an exception to it.</summary>
    public async Task<LessonResponse> UpdateAsync(Guid id, UpdateLessonRequest request)
    {
        var lesson = await FindAsync(id);
        EnsureStatus(lesson, LessonStatus.Scheduled, "Only scheduled lessons can be moved.");

        var timeChanged = lesson.LocalDate != request.Date || lesson.LocalStartTime != request.StartTime;
        lesson.SetTime(request.Date, request.StartTime, request.DurationMinutes, lesson.TimeZoneId);
        lesson.IsModifiedFromSeries = lesson.SeriesId is not null;
        if (timeChanged)
        {
            // The student confirmed the old time, not the new one.
            lesson.Attendance = AttendanceResponse.Unknown;
            lesson.AttendanceRespondedAt = null;
        }

        await EnsureNoOverlapAsync(lesson);
        await SaveAsync();
        return await GetAsync(lesson.Id);
    }

    public async Task<LessonResponse> CancelAsync(Guid id)
    {
        var lesson = await FindAsync(id);
        EnsureStatus(lesson, LessonStatus.Scheduled, "Only scheduled lessons can be cancelled.");

        lesson.Status = LessonStatus.Cancelled;
        lesson.IsModifiedFromSeries = lesson.SeriesId is not null;
        await SaveAsync();
        return await GetAsync(lesson.Id);
    }

    public async Task<LessonResponse> CompleteAsync(Guid id)
    {
        var lesson = await FindAsync(id);
        EnsureStatus(lesson, LessonStatus.Scheduled, "Only scheduled lessons can be marked as completed.");
        if (lesson.StartUtc > time.GetUtcNow().UtcDateTime)
        {
            throw ApiException.Conflict("A lesson cannot be completed before it starts.");
        }

        lesson.Status = LessonStatus.Completed;
        await SaveAsync();
        return await GetAsync(lesson.Id);
    }

    /// <summary>Marks every scheduled lesson that has already ended as completed.</summary>
    public async Task<CompletePastResponse> CompletePastAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var completed = await db.Lessons
            .Where(l => l.Status == LessonStatus.Scheduled && l.EndUtc <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Status, LessonStatus.Completed));
        return new CompletePastResponse(completed);
    }

    /// <summary>One note per lesson; saving empty content removes the note.</summary>
    public async Task<LessonResponse> SaveNoteAsync(Guid lessonId, SaveNoteRequest request)
    {
        var lesson = await db.Lessons.Include(l => l.Note).FirstOrDefaultAsync(l => l.Id == lessonId)
            ?? throw ApiException.NotFound("Lesson");
        var content = request.Content.Trim();
        var now = time.GetUtcNow().UtcDateTime;

        if (content.Length == 0)
        {
            if (lesson.Note is not null)
            {
                db.LessonNotes.Remove(lesson.Note);
            }
        }
        else if (lesson.Note is null)
        {
            db.LessonNotes.Add(new LessonNote
            {
                TutorId = lesson.TutorId,
                LessonId = lesson.Id,
                Content = content,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            lesson.Note.Content = content;
            lesson.Note.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        return await GetAsync(lesson.Id);
    }

    public async Task<List<StudentNoteResponse>> GetStudentNotesAsync(Guid studentId)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            throw ApiException.NotFound("Student");
        }

        return await db.Lessons
            .Where(l => l.StudentId == studentId && l.Note != null)
            .OrderByDescending(l => l.StartUtc)
            .Select(l => new StudentNoteResponse(l.Id, l.LocalDate, l.LocalStartTime, l.Note!.Content, l.Note.UpdatedAt))
            .ToListAsync();
    }

    public async Task<string> GetTutorTimeZoneAsync() =>
        await db.Users.Where(u => u.Id == currentTutor.RequiredId).Select(u => u.TimeZoneId).SingleAsync();

    /// <summary>
    /// Checks for an overlapping lesson to return a readable error. The exclusion constraint in the database
    /// is what actually guarantees it, also for two concurrent requests.
    /// </summary>
    public async Task EnsureNoOverlapAsync(Lesson lesson)
    {
        var clash = await db.Lessons
            .Where(l => l.Id != lesson.Id
                && l.Status != LessonStatus.Cancelled
                && l.StartUtc < lesson.EndUtc
                && l.EndUtc > lesson.StartUtc)
            .Select(l => new { l.LocalDate, l.LocalStartTime, StudentName = l.Student!.FullName })
            .FirstOrDefaultAsync();

        if (clash is not null)
        {
            throw OverlapConflict($"It overlaps with {clash.StudentName} on {clash.LocalDate:yyyy-MM-dd} at {clash.LocalStartTime:HH\\:mm}.");
        }
    }

    public static ApiException OverlapConflict(string? detail = null) =>
        ApiException.Conflict("This lesson overlaps with another lesson.", detail);

    private async Task<Lesson> FindAsync(Guid id) =>
        await db.Lessons.FirstOrDefaultAsync(l => l.Id == id) ?? throw ApiException.NotFound("Lesson");

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (DbErrors.IsExclusionViolation(exception))
        {
            throw OverlapConflict();
        }
    }

    private static void EnsureStatus(Lesson lesson, LessonStatus expected, string message)
    {
        if (lesson.Status != expected)
        {
            throw ApiException.Conflict(message, $"The lesson is {lesson.Status.ToString().ToLowerInvariant()}.");
        }
    }

    private static readonly System.Linq.Expressions.Expression<Func<Lesson, LessonResponse>> ToResponse = l =>
        new LessonResponse(
            l.Id, l.StudentId, l.Student!.FullName, l.SeriesId,
            l.LocalDate, l.LocalStartTime, l.DurationMinutes, l.StartUtc, l.EndUtc,
            l.Status, l.Attendance, l.AttendanceRespondedAt, l.PricePerLesson, l.IsModifiedFromSeries,
            l.Note != null ? l.Note.Content : null);
}
