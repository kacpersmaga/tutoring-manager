using TutoringManager.Api.Lessons;

namespace TutoringManager.Tests;

public class LessonTimeTests
{
    private const string Warsaw = "Europe/Warsaw";

    [Theory]
    [InlineData("2026-03-24", 16)] // Tuesday before spring forward (29 March): UTC+1
    [InlineData("2026-03-31", 15)] // Tuesday after: UTC+2
    [InlineData("2026-10-20", 15)] // Tuesday before fall back (25 October): UTC+2
    [InlineData("2026-10-27", 16)] // Tuesday after: UTC+1
    public void Lesson_at_17_local_stays_at_17_across_daylight_saving_changes(string date, int expectedUtcHour)
    {
        var utc = LessonTime.ToUtc(DateOnly.Parse(date), new TimeOnly(17, 0), Warsaw);

        Assert.Equal(expectedUtcHour, utc.Hour);
        Assert.Equal(17, LessonTime.ToLocal(utc, Warsaw).Hour);
    }

    [Fact]
    public void Time_in_the_spring_forward_gap_is_moved_forward()
    {
        // 02:30 on 29 March 2026 does not exist in Warsaw; clocks jump from 02:00 to 03:00.
        var utc = LessonTime.ToUtc(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), Warsaw);

        Assert.Equal(new DateTime(2026, 3, 29, 1, 0, 0), utc); // 03:00 local
    }

    [Fact]
    public void Series_occurrences_respect_day_of_week_and_end_date()
    {
        var series = new LessonSeries
        {
            DayOfWeek = DayOfWeek.Wednesday,
            StartDate = new DateOnly(2027, 1, 4), // a Monday
            EndDate = new DateOnly(2027, 1, 27),
        };

        var dates = series.OccurrencesBetween(new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 1));

        Assert.Equal(
            [new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 13), new DateOnly(2027, 1, 20), new DateOnly(2027, 1, 27)],
            dates);
    }
}
