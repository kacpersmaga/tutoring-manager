namespace TutoringManager.Api.Lessons;

/// <summary>
/// A weekly lesson (same day and time). The series generates concrete <see cref="Lesson"/> rows
/// <see cref="HorizonWeeks"/> weeks ahead; notes and attendance belong to those rows.
/// </summary>
public class LessonSeries
{
    public const int HorizonWeeks = 12;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid StudentId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string TimeZoneId { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Lessons exist for every occurrence up to and including this date.</summary>
    public DateOnly GeneratedUntil { get; set; }

    /// <summary>Dates of the occurrences in [from, until], limited by the series' start and end dates.</summary>
    public IEnumerable<DateOnly> OccurrencesBetween(DateOnly from, DateOnly until)
    {
        var first = from > StartDate ? from : StartDate;
        var last = EndDate is { } end && end < until ? end : until;

        var date = first.AddDays(((int)DayOfWeek - (int)first.DayOfWeek + 7) % 7);
        for (; date <= last; date = date.AddDays(7))
        {
            yield return date;
        }
    }

    public static DateOnly HorizonFrom(DateOnly today) => today.AddDays(HorizonWeeks * 7);
}
