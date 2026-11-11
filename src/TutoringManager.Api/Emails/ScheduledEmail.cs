using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutoringManager.Api.Lessons;

namespace TutoringManager.Api.Emails;

public enum EmailType
{
    AttendanceRequest,
}

public enum EmailStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
    Cancelled,
}

/// <summary>
/// An email waiting to be sent. The table is the queue: <see cref="EmailDispatcher"/> claims rows with
/// FOR UPDATE SKIP LOCKED, so several app instances never pick the same row.
/// </summary>
public class ScheduledEmail
{
    public const int MaxAttempts = 5;

    /// <summary>Wait before attempt 2, 3, 4 and 5.</summary>
    public static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(25),
        TimeSpan.FromMinutes(60),
    ];

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid? LessonId { get; set; }
    public EmailType Type { get; set; }
    public string ToEmail { get; set; } = "";
    public DateTime SendAtUtc { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }

    /// <summary>Lease: while in the future, the row belongs to the instance that claimed it.</summary>
    public DateTime? LockedUntilUtc { get; set; }

    public string? LastError { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public void MarkSent(DateTime now)
    {
        Status = EmailStatus.Sent;
        SentAtUtc = now;
        LockedUntilUtc = null;
        LastError = null;
    }

    public void MarkFailedAttempt(DateTime now, string error)
    {
        LastError = error;
        LockedUntilUtc = null;
        if (Attempts >= MaxAttempts)
        {
            Status = EmailStatus.Failed;
            return;
        }

        Status = EmailStatus.Pending;
        NextAttemptAtUtc = now + RetryDelays[Attempts - 1];
    }

    public void MarkCancelled()
    {
        Status = EmailStatus.Cancelled;
        LockedUntilUtc = null;
    }
}

public class ScheduledEmailConfiguration : IEntityTypeConfiguration<ScheduledEmail>
{
    public void Configure(EntityTypeBuilder<ScheduledEmail> email)
    {
        email.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
        email.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        email.Property(e => e.ToEmail).HasMaxLength(256);
        email.Property(e => e.LastError).HasMaxLength(500);
        email.HasOne<Lesson>().WithMany(l => l.Emails).HasForeignKey(e => e.LessonId).OnDelete(DeleteBehavior.Cascade);
        // Serves the dispatcher's "what is due" query.
        email.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
    }
}
