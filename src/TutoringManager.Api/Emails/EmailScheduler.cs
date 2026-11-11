using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Emails;

public class EmailScheduler(AppDbContext db, TimeProvider time)
{
    private static readonly TimeSpan SendBeforeLesson = TimeSpan.FromHours(24);

    /// <summary>
    /// Queues the "are you coming?" email for 24 hours before the lesson, or right away when the lesson
    /// is sooner than that. Added to the context only: it is saved together with the lesson.
    /// </summary>
    public void ScheduleAttendanceRequest(Lesson lesson, string studentEmail)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (lesson.StartUtc <= now)
        {
            return;
        }

        var sendAt = lesson.StartUtc - SendBeforeLesson;
        if (sendAt < now)
        {
            sendAt = now;
        }

        db.ScheduledEmails.Add(new ScheduledEmail
        {
            TutorId = lesson.TutorId,
            LessonId = lesson.Id,
            Type = EmailType.AttendanceRequest,
            ToEmail = studentEmail,
            SendAtUtc = sendAt,
            NextAttemptAtUtc = sendAt,
        });
    }

    public async Task CancelPendingAsync(Guid lessonId) =>
        await db.ScheduledEmails
            .IgnoreQueryFilters()
            .Where(e => e.LessonId == lessonId && e.Status == EmailStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Status, EmailStatus.Cancelled));
}
