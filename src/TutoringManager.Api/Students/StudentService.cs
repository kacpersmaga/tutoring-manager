using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;

namespace TutoringManager.Api.Students;

public class StudentService(AppDbContext db, CurrentTutor currentTutor, TimeProvider time)
{
    public async Task<List<StudentSummary>> ListAsync(bool includeInactive) =>
        await db.Students
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.FullName)
            .Select(s => new StudentSummary(s.Id, s.FullName, s.Email, s.RatePerLesson, s.IsActive))
            .ToListAsync();

    public async Task<StudentDetails> GetAsync(Guid id) => ToDetails(await FindAsync(id));

    public async Task<StudentDetails> CreateAsync(CreateStudentRequest request)
    {
        var student = new Student
        {
            TutorId = currentTutor.RequiredId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            RatePerLesson = request.RatePerLesson,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };

        foreach (var raw in request.BankAccounts ?? [])
        {
            var number = await ValidateNewAccountAsync(raw);
            if (student.BankAccounts.All(a => a.AccountNumber != number))
            {
                student.BankAccounts.Add(new StudentBankAccount { TutorId = student.TutorId, AccountNumber = number });
            }
        }

        db.Students.Add(student);
        await db.SaveChangesAsync();
        return ToDetails(student);
    }

    public async Task<StudentDetails> UpdateAsync(Guid id, UpdateStudentRequest request)
    {
        var student = await FindAsync(id);
        student.FullName = request.FullName.Trim();
        student.Email = request.Email.Trim();
        student.RatePerLesson = request.RatePerLesson;
        student.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return ToDetails(student);
    }

    /// <summary>
    /// GDPR erasure: removes the student's lessons, notes, tokens and bank accounts.
    /// Bank transactions stay (they are the tutor's records) but are unassigned.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var student = await FindAsync(id);
        db.Students.Remove(student);
        await db.SaveChangesAsync();
    }

    public async Task<BankAccountResponse> AddBankAccountAsync(Guid studentId, AddBankAccountRequest request)
    {
        var student = await FindAsync(studentId);
        var account = new StudentBankAccount
        {
            TutorId = student.TutorId,
            StudentId = student.Id,
            AccountNumber = await ValidateNewAccountAsync(request.AccountNumber),
        };

        db.StudentBankAccounts.Add(account);
        await db.SaveChangesAsync();
        return new BankAccountResponse(account.Id, account.AccountNumber);
    }

    public async Task DeleteBankAccountAsync(Guid studentId, Guid accountId)
    {
        var account = await db.StudentBankAccounts.FirstOrDefaultAsync(a => a.Id == accountId && a.StudentId == studentId)
            ?? throw ApiException.NotFound("Bank account");
        db.StudentBankAccounts.Remove(account);
        await db.SaveChangesAsync();
    }

    private async Task<Student> FindAsync(Guid id) =>
        await db.Students.Include(s => s.BankAccounts).FirstOrDefaultAsync(s => s.Id == id)
        ?? throw ApiException.NotFound("Student");

    private async Task<string> ValidateNewAccountAsync(string raw)
    {
        var number = BankAccountNumber.Normalize(raw);
        if (!BankAccountNumber.IsValid(number))
        {
            throw ApiException.Validation("accountNumber", $"\"{raw}\" is not a valid 26-digit Polish account number.");
        }

        if (await db.StudentBankAccounts.AnyAsync(a => a.AccountNumber == number))
        {
            throw ApiException.Conflict("This account number already belongs to one of your students.");
        }

        return number;
    }

    private static StudentDetails ToDetails(Student s) => new(
        s.Id, s.FullName, s.Email, s.RatePerLesson, s.IsActive, s.CreatedAt,
        s.BankAccounts.OrderBy(a => a.AccountNumber).Select(a => new BankAccountResponse(a.Id, a.AccountNumber)).ToList());
}
