using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Emails;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Lessons;

public class SeriesService(
    AppDbContext db,
    CurrentTutor currentTutor,
    LessonService lessons,
    EmailScheduler emails,
    TimeProvider time,
    ILogger<SeriesService> logger)
{
    public async Task<List<SeriesResponse>> ListAsync(Guid? studentId) =>
        await db.LessonSeries
            .Where(s => studentId == null || s.StudentId == studentId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .Select(s => ToResponse(s))
            .ToListAsync();

    public async Task<SeriesResponse> GetAsync(Guid id) => ToResponse(await FindAsync(id));

    /// <summary>Creates the series and its lessons up to 12 weeks ahead. Fails as a whole if any of them overlaps.</summary>
    public async Task<SeriesResponse> CreateAsync(CreateSeriesRequest request)
    {
        ValidateEndDate(request.StartDate, request.EndDate);
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId)
            ?? throw ApiException.Validation("studentId", "Student not found.");

        var series = new LessonSeries
        {
            TutorId = currentTutor.RequiredId,
            StudentId = student.Id,
            DayOfWeek = request.StartDate.DayOfWeek,
            StartTime = request.StartTime,
            DurationMinutes = request.DurationMinutes,
            TimeZoneId = await lessons.GetTutorTimeZoneAsync(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };
        db.LessonSeries.Add(series);

        // Past occurrences are created too, so a tutor can enter a series that has already been running.
        await GenerateAsync(series, series.StartDate, Horizon(series), student, skipConflicts: false, skipPast: false);
        await db.SaveChangesAsync();
        return ToResponse(series);
    }

    /// <summary>
    /// Changes the time of all future lessons that were not moved, cancelled or annotated individually.
    /// Past lessons and individual exceptions stay as they are.
    /// </summary>
    public async Task<SeriesResponse> UpdateAsync(Guid id, UpdateSeriesRequest request)
    {
        var series = await FindAsync(id);
        ValidateEndDate(series.StartDate, request.EndDate);
        var student = await db.Students.SingleAsync(s => s.Id == series.StudentId);

        await using var transaction = await db.Database.BeginTransactionAsync();

        // Delete first and save, so the regenerated lessons do not collide with the ones they replace.
        db.Lessons.RemoveRange(await FutureUnmodifiedLessons(series.Id).ToListAsync());
        await db.SaveChangesAsync();

        series.DayOfWeek = request.DayOfWeek;
        series.StartTime = request.StartTime;
        series.DurationMinutes = request.DurationMinutes;
        series.EndDate = request.EndDate;

        await GenerateAsync(series, Today(series), Horizon(series), student, skipConflicts: false, skipPast: true);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return ToResponse(series);
    }

    /// <summary>Ends the series: future unmodified lessons are removed, past lessons and exceptions stay.</summary>
    public async Task DeleteAsync(Guid id)
    {
        var series = await FindAsync(id);
        await FutureUnmodifiedLessons(series.Id).ExecuteDeleteAsync();
        db.LessonSeries.Remove(series);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Run daily by <see cref="SeriesExtensionWorker"/>: tops every series up to 12 weeks ahead.
    /// An occurrence that collides with another lesson is skipped and logged.
    /// </summary>
    public async Task<int> ExtendAllAsync(DateOnly today)
    {
        var horizon = LessonSeries.HorizonFrom(today);
        var due = await db.LessonSeries
            .IgnoreQueryFilters()
            .Where(s => s.GeneratedUntil < horizon && (s.EndDate == null || s.GeneratedUntil < s.EndDate))
            .ToListAsync();

        var created = 0;
        foreach (var series in due)
        {
            var student = await db.Students.IgnoreQueryFilters().SingleAsync(s => s.Id == series.StudentId);
            created += await GenerateAsync(series, series.GeneratedUntil.AddDays(1), horizon, student, skipConflicts: true, skipPast: true);
            await db.SaveChangesAsync();
        }

        return created;
    }

    private async Task<int> GenerateAsync(
        LessonSeries series, DateOnly from, DateOnly horizon, Student student, bool skipConflicts, bool skipPast)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var existingOccurrences = await db.Lessons.IgnoreQueryFilters()
            .Where(l => l.SeriesId == series.Id && l.SeriesOccurrenceDate != null)
            .Select(l => l.SeriesOccurrenceDate!.Value)
            .ToListAsync();

        var created = 0;
        foreach (var date in series.OccurrencesBetween(from, horizon))
        {
            if (existingOccurrences.Contains(date))
            {
                continue;
            }

            var lesson = new Lesson
            {
                TutorId = series.TutorId,
                StudentId = series.StudentId,
                SeriesId = series.Id,
                SeriesOccurrenceDate = date,
                PricePerLesson = student.RatePerLesson,
            };
            lesson.SetTime(date, series.StartTime, series.DurationMinutes, series.TimeZoneId);

            if (skipPast && lesson.StartUtc <= now)
            {
                continue;
            }

            try
            {
                await lessons.EnsureNoOverlapAsync(lesson);
            }
            catch (ApiException) when (skipConflicts)
            {
                logger.LogWarning("Skipped occurrence {Date} of series {SeriesId}: it overlaps another lesson", date, series.Id);
                continue;
            }

            db.Lessons.Add(lesson);
            emails.ScheduleAttendanceRequest(lesson, student.Email);
            created++;
        }

        if (horizon > series.GeneratedUntil)
        {
            series.GeneratedUntil = horizon;
        }

        return created;
    }

    private IQueryable<Lesson> FutureUnmodifiedLessons(Guid seriesId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return db.Lessons.Where(l => l.SeriesId == seriesId
            && l.StartUtc > now
            && l.Status == LessonStatus.Scheduled
            && !l.IsModifiedFromSeries
            && l.Note == null);
    }

    private async Task<LessonSeries> FindAsync(Guid id) =>
        await db.LessonSeries.FirstOrDefaultAsync(s => s.Id == id) ?? throw ApiException.NotFound("Series");

    private DateOnly Today(LessonSeries series) => LessonTime.Today(time, series.TimeZoneId);

    private DateOnly Horizon(LessonSeries series) => LessonSeries.HorizonFrom(Today(series));

    private static void ValidateEndDate(DateOnly startDate, DateOnly? endDate)
    {
        if (endDate < startDate)
        {
            throw ApiException.Validation("endDate", "The end date cannot be before the start date.");
        }
    }

    private static SeriesResponse ToResponse(LessonSeries s) =>
        new(s.Id, s.StudentId, s.DayOfWeek, s.StartTime, s.DurationMinutes, s.StartDate, s.EndDate);
}
