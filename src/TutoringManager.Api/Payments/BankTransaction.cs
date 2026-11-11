using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Payments;

public class BankImport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public string FileName { get; set; } = "";

    /// <summary>Hash of the file's bytes: the same file cannot be imported twice.</summary>
    public string FileSha256 { get; set; } = "";
    public string BankFormat { get; set; } = "";
    public DateTime ImportedAt { get; set; }
}

public enum MatchedBy
{
    None,
    Account,
    Manual,
}

/// <summary>An incoming transfer from a bank statement. Only income is imported.</summary>
public class BankTransaction
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid ImportId { get; set; }
    public DateOnly BookingDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PLN";
    public string CounterpartyAccount { get; set; } = "";
    public string CounterpartyName { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>
    /// Identifies the transfer across statements (see <see cref="TransactionKeys"/>), so overlapping
    /// statements do not count the same payment twice.
    /// </summary>
    public string TransactionKey { get; set; } = "";

    public Guid? StudentId { get; set; }
    public MatchedBy MatchedBy { get; set; }

    public Student? Student { get; set; }

    public void AssignTo(Guid studentId, MatchedBy matchedBy)
    {
        StudentId = studentId;
        MatchedBy = matchedBy;
    }
}

public class BankImportConfiguration : IEntityTypeConfiguration<BankImport>
{
    public void Configure(EntityTypeBuilder<BankImport> import)
    {
        import.Property(i => i.FileName).HasMaxLength(255);
        import.Property(i => i.FileSha256).HasMaxLength(64);
        import.Property(i => i.BankFormat).HasMaxLength(40);
        import.HasIndex(i => new { i.TutorId, i.FileSha256 }).IsUnique();
    }
}

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> transaction)
    {
        transaction.Property(t => t.Amount).HasPrecision(12, 2);
        transaction.Property(t => t.Currency).HasMaxLength(3);
        transaction.Property(t => t.CounterpartyAccount).HasMaxLength(34);
        transaction.Property(t => t.CounterpartyName).HasMaxLength(300);
        transaction.Property(t => t.Title).HasMaxLength(500);
        transaction.Property(t => t.TransactionKey).HasMaxLength(64);
        transaction.Property(t => t.MatchedBy).HasConversion<string>().HasMaxLength(20);

        transaction.HasIndex(t => new { t.TutorId, t.TransactionKey }).IsUnique();
        transaction.HasIndex(t => new { t.TutorId, t.StudentId, t.BookingDate });
        transaction.HasOne<BankImport>().WithMany().HasForeignKey(t => t.ImportId).OnDelete(DeleteBehavior.Cascade);
        // Erasing a student keeps the tutor's bank records but unassigns them.
        transaction.HasOne(t => t.Student).WithMany().HasForeignKey(t => t.StudentId).OnDelete(DeleteBehavior.SetNull);
    }
}
