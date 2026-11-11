using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Payments;

public record TransactionResponse(
    Guid Id,
    DateOnly BookingDate,
    decimal Amount,
    string Currency,
    string CounterpartyAccount,
    string CounterpartyName,
    string Title,
    Guid? StudentId,
    string? StudentName,
    MatchedBy MatchedBy);

public record AssignTransactionRequest(Guid StudentId, bool RememberAccount);

public class TransactionService(AppDbContext db)
{
    public async Task<List<TransactionResponse>> GetUnmatchedAsync() =>
        await db.BankTransactions
            .Where(t => t.StudentId == null)
            .OrderByDescending(t => t.BookingDate)
            .Select(ToResponse)
            .ToListAsync();

    public async Task<List<TransactionResponse>> GetForStudentAsync(Guid studentId)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId))
        {
            throw ApiException.NotFound("Student");
        }

        return await db.BankTransactions
            .Where(t => t.StudentId == studentId)
            .OrderByDescending(t => t.BookingDate)
            .Select(ToResponse)
            .ToListAsync();
    }

    /// <summary>
    /// Assigns a transfer by hand, e.g. one sent by a parent. With <paramref name="request"/>.RememberAccount the
    /// sender's account is added to the student, and other unmatched transfers from it are assigned too.
    /// </summary>
    public async Task<TransactionResponse> AssignAsync(Guid transactionId, AssignTransactionRequest request)
    {
        var transaction = await db.BankTransactions.FirstOrDefaultAsync(t => t.Id == transactionId)
            ?? throw ApiException.NotFound("Transaction");
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId)
            ?? throw ApiException.Validation("studentId", "Student not found.");

        transaction.AssignTo(student.Id, MatchedBy.Manual);

        if (request.RememberAccount && BankAccountNumber.IsValid(transaction.CounterpartyAccount))
        {
            var owner = await db.StudentBankAccounts.FirstOrDefaultAsync(a => a.AccountNumber == transaction.CounterpartyAccount);
            if (owner is not null && owner.StudentId != student.Id)
            {
                throw ApiException.Conflict("This account is already remembered for another student.");
            }

            if (owner is null)
            {
                db.StudentBankAccounts.Add(new StudentBankAccount
                {
                    TutorId = student.TutorId,
                    StudentId = student.Id,
                    AccountNumber = transaction.CounterpartyAccount,
                });
            }

            var sameSender = await db.BankTransactions
                .Where(t => t.StudentId == null && t.CounterpartyAccount == transaction.CounterpartyAccount && t.Id != transaction.Id)
                .ToListAsync();
            sameSender.ForEach(t => t.AssignTo(student.Id, MatchedBy.Account));
        }

        await db.SaveChangesAsync();
        return await db.BankTransactions.Where(t => t.Id == transaction.Id).Select(ToResponse).SingleAsync();
    }

    private static readonly Expression<Func<BankTransaction, TransactionResponse>> ToResponse = t => new TransactionResponse(
        t.Id, t.BookingDate, t.Amount, t.Currency, t.CounterpartyAccount, t.CounterpartyName, t.Title,
        t.StudentId, t.Student != null ? t.Student.FullName : null, t.MatchedBy);
}
