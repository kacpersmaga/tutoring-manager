using TutoringManager.Api.Students;

namespace TutoringManager.Api.Lessons;

public enum LessonStatus
{
    Scheduled,
    Completed,
    Cancelled,
}

public enum AttendanceResponse
{
    Unknown,
    Confirmed,
    Declined,
}

public class Lesson
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? SeriesId { get; set; }

    /// <summary>The series date this lesson was generated for. Stays the same when the lesson is moved,
    /// so regenerating the series does not create a second lesson for that week.</summary>
    public DateOnly? SeriesOccurrenceDate { get; set; }

    // Local date, time and time zone are the source of truth; StartUtc/EndUtc are derived for queries.
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalStartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.Scheduled;
    public AttendanceResponse Attendance { get; set; } = AttendanceResponse.Unknown;
    public DateTime? AttendanceRespondedAt { get; set; }

    /// <summary>The student's rate when the lesson was created, so a later rate change does not rewrite history.</summary>
    public decimal PricePerLesson { get; set; }

    /// <summary>Moved or cancelled individually, so editing the series leaves it alone.</summary>
    public bool IsModifiedFromSeries { get; set; }

    /// <summary>Maps to PostgreSQL's xmin system column: optimistic concurrency without an extra column.</summary>
    public uint Version { get; set; }

    public Student? Student { get; set; }
    public LessonNote? Note { get; set; }

    public void SetTime(DateOnly localDate, TimeOnly localStartTime, int durationMinutes, string timeZoneId)
    {
        LocalDate = localDate;
        LocalStartTime = localStartTime;
        DurationMinutes = durationMinutes;
        TimeZoneId = timeZoneId;
        StartUtc = LessonTime.ToUtc(localDate, localStartTime, timeZoneId);
        EndUtc = StartUtc.AddMinutes(durationMinutes);
    }
}

public class LessonNote
{
    public const int MaxLength = 4000;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid LessonId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
