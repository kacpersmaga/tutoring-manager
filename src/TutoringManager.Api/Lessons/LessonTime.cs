namespace TutoringManager.Api.Lessons;

public static class LessonTime
{
    /// <summary>
    /// Converts a wall-clock time in the tutor's time zone to UTC. "Every Tuesday at 17:00" stays at 17:00
    /// local time across daylight saving changes, because the UTC value is recomputed for each date.
    /// </summary>
    public static DateTime ToUtc(DateOnly localDate, TimeOnly localTime, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = localDate.ToDateTime(localTime, DateTimeKind.Unspecified);

        // A time inside the spring-forward gap (e.g. 02:30 on the last Sunday of March in Warsaw)
        // does not exist; move it forward the way clocks do.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static DateTime ToLocal(DateTime utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    public static DateOnly Today(TimeProvider time, string timeZoneId) =>
        DateOnly.FromDateTime(ToLocal(time.GetUtcNow().UtcDateTime, timeZoneId));
}
