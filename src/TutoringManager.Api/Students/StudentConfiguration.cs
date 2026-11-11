using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutoringManager.Api.Auth;

namespace TutoringManager.Api.Students;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> student)
    {
        student.Property(s => s.FullName).HasMaxLength(200);
        student.Property(s => s.Email).HasMaxLength(256);
        student.Property(s => s.RatePerLesson).HasPrecision(10, 2);
        student.HasOne<Tutor>().WithMany().HasForeignKey(s => s.TutorId);
        student.HasIndex(s => new { s.TutorId, s.FullName });
        student.HasMany(s => s.BankAccounts).WithOne().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StudentBankAccountConfiguration : IEntityTypeConfiguration<StudentBankAccount>
{
    public void Configure(EntityTypeBuilder<StudentBankAccount> account)
    {
        account.Property(a => a.AccountNumber).HasMaxLength(34);
        account.HasIndex(a => new { a.TutorId, a.AccountNumber }).IsUnique();
    }
}
