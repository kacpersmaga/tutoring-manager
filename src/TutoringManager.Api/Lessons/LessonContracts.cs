using System.ComponentModel.DataAnnotations;
using TutoringManager.Api.Emails;

namespace TutoringManager.Api.Lessons;

public record CreateLessonRequest(
    Guid StudentId,
    DateOnly Date,
    TimeOnly StartTime,
    [property: Range(15, 480)] int DurationMinutes);

public record UpdateLessonRequest(
    DateOnly Date,
    TimeOnly StartTime,
    [property: Range(15, 480)] int DurationMinutes);

public record SaveNoteRequest([property: MaxLength(LessonNote.MaxLength)] string Content);

public record LessonResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid? SeriesId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    DateTime StartUtc,
    DateTime EndUtc,
    LessonStatus Status,
    AttendanceResponse Attendance,
    DateTime? AttendanceRespondedAt,
    decimal PricePerLesson,
    bool IsModifiedFromSeries,
    string? Note,
    EmailStatus? EmailStatus);

public record StudentNoteResponse(Guid LessonId, DateOnly Date, TimeOnly StartTime, string Content, DateTime UpdatedAt);

/// <summary>One page of a student's notes, newest first. <see cref="HasMore"/> tells the UI to offer older ones.</summary>
public record StudentNotesPage(List<StudentNoteResponse> Notes, bool HasMore);

/// <summary>A lesson that can still get a note: already started, not cancelled, no note yet.</summary>
public record LessonWithoutNote(Guid LessonId, DateOnly Date, TimeOnly StartTime);

public record CompletePastResponse(int Completed);

public record CreateSeriesRequest(
    Guid StudentId,
    DateOnly StartDate,
    TimeOnly StartTime,
    [property: Range(15, 480)] int DurationMinutes,
    DateOnly? EndDate);

public record UpdateSeriesRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    [property: Range(15, 480)] int DurationMinutes,
    DateOnly? EndDate);

public record SeriesResponse(
    Guid Id,
    Guid StudentId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    int DurationMinutes,
    DateOnly StartDate,
    DateOnly? EndDate);
