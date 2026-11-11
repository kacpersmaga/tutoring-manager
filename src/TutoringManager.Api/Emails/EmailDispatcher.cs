using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TutoringManager.Api.Attendance;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Emails;

/// <summary>
/// Sends due emails. Safe to run on several instances at once: each batch is claimed with
/// FOR UPDATE SKIP LOCKED and leased for 5 minutes. If an instance dies mid-batch the lease expires
/// and another instance picks the email up again, so delivery is at-least-once, not exactly-once.
/// </summary>
public class EmailDispatcher(
    AppDbContext db,
    AttendanceService attendance,
    IEmailSender sender,
    IOptions<EmailOptions> options,
    TimeProvider time,
    ILogger<EmailDispatcher> logger)
{
    public const int BatchSize = 20;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>Claims and sends one batch. Returns how many emails were claimed.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var batch = await ClaimBatchAsync();
        foreach (var email in batch)
        {
            await ProcessAsync(email, cancellationToken);
        }

        return batch.Count;
    }

    /// <summary>
    /// Atomically moves up to <see cref="BatchSize"/> due emails to Processing. Rows locked by another
    /// instance are skipped, not waited for. Rows whose lease expired (their instance crashed) are due again.
    /// </summary>
    public async Task<List<ScheduledEmail>> ClaimBatchAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var lockedUntil = now + Lease;

        return await db.ScheduledEmails
            .FromSql($"""
                UPDATE scheduled_emails
                SET status = 'Processing', locked_until_utc = {lockedUntil}, attempts = attempts + 1
                WHERE id IN (
                    SELECT id FROM scheduled_emails
                    WHERE (status = 'Pending' AND next_attempt_at_utc <= {now})
                       OR (status = 'Processing' AND locked_until_utc < {now})
                    ORDER BY send_at_utc
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    private async Task ProcessAsync(ScheduledEmail email, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var lesson = await db.Lessons.IgnoreQueryFilters()
            .Include(l => l.Student)
            .SingleOrDefaultAsync(l => l.Id == email.LessonId, cancellationToken);

        if (lesson is null || lesson.Status != LessonStatus.Scheduled || lesson.StartUtc <= now)
        {
            email.MarkCancelled();
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var tutorName = await db.Users.Where(u => u.Id == lesson.TutorId).Select(u => u.DisplayName).SingleAsync(cancellationToken);
            // The token is saved before sending: a link in an email must always work.
            var token = await attendance.CreateTokenAsync(lesson);
            var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/a/{token}";

            email.ToEmail = lesson.Student!.Email;
            await sender.SendAsync(
                AttendanceEmail.Create(email.ToEmail, tutorName, lesson.LocalDate, lesson.LocalStartTime, link),
                cancellationToken);
            email.MarkSent(time.GetUtcNow().UtcDateTime);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Only the exception type is logged: SMTP errors can echo the recipient's address.
            logger.LogWarning("Email {EmailId} failed on attempt {Attempt}: {ErrorType}", email.Id, email.Attempts, exception.GetType().Name);
            email.MarkFailedAttempt(time.GetUtcNow().UtcDateTime, Truncate(exception.Message, 500));
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
