namespace TutoringManager.Api.Students;

public class Student
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal RatePerLesson { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public List<StudentBankAccount> BankAccounts { get; set; } = [];
}

public class StudentBankAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TutorId { get; set; }
    public Guid StudentId { get; set; }

    /// <summary>Digits only, see <see cref="BankAccountNumber.Normalize"/>.</summary>
    public string AccountNumber { get; set; } = "";
}
