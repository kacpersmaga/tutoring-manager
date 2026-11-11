using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TutoringManager.Api.Infrastructure;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Payments;

public record ImportResult(
    Guid ImportId,
    string BankFormat,
    int IncomingTransactions,
    int Imported,
    int SkippedAsDuplicates,
    int Matched,
    int Unmatched);

public class ImportService(
    AppDbContext db,
    CurrentTutor currentTutor,
    IEnumerable<IBankStatementParser> parsers,
    TimeProvider time)
{
    public const long MaxFileBytes = 1024 * 1024;

    public async Task<ImportResult> ImportAsync(IFormFile? file)
    {
        var bytes = await ReadValidatedAsync(file);
        var tutorId = currentTutor.RequiredId;
        var fileHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (await db.BankImports.AnyAsync(i => i.FileSha256 == fileHash))
        {
            throw ApiException.Conflict("This file has already been imported.");
        }

        var content = StatementText.Decode(bytes);
        var parser = parsers.FirstOrDefault(p => p.CanParse(content))
            ?? throw ApiException.Validation("file", "Unrecognised statement format. Supported banks: " +
                string.Join(", ", parsers.Select(p => p.BankFormat)) + ".");
        var incoming = parser.Parse(content).Where(t => t.Amount > 0).ToList();

        var import = new BankImport
        {
            TutorId = tutorId,
            FileName = Path.GetFileName(file!.FileName),
            FileSha256 = fileHash,
            BankFormat = parser.BankFormat,
            ImportedAt = time.GetUtcNow().UtcDateTime,
        };
        db.BankImports.Add(import);

        var transactions = BuildTransactions(incoming, import);
        var keys = transactions.Select(t => t.TransactionKey).ToList();
        var existingKeys = await db.BankTransactions
            .Where(t => keys.Contains(t.TransactionKey))
            .Select(t => t.TransactionKey)
            .ToListAsync();
        var newTransactions = transactions.Where(t => !existingKeys.Contains(t.TransactionKey)).ToList();

        var studentsByAccount = await db.StudentBankAccounts.ToDictionaryAsync(a => a.AccountNumber, a => a.StudentId);
        foreach (var transaction in newTransactions)
        {
            if (studentsByAccount.TryGetValue(transaction.CounterpartyAccount, out var studentId))
            {
                transaction.AssignTo(studentId, MatchedBy.Account);
            }
        }

        db.BankTransactions.AddRange(newTransactions);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw ApiException.Conflict("This statement is being imported right now. Refresh in a moment.");
        }

        var matched = newTransactions.Count(t => t.StudentId is not null);
        return new ImportResult(import.Id, parser.BankFormat, incoming.Count, newTransactions.Count,
            transactions.Count - newTransactions.Count, matched, newTransactions.Count - matched);
    }

    private static List<BankTransaction> BuildTransactions(List<ParsedTransaction> incoming, BankImport import)
    {
        var occurrences = new Dictionary<ParsedTransaction, int>();
        var transactions = new List<BankTransaction>();
        foreach (var parsed in incoming)
        {
            var occurrence = occurrences.GetValueOrDefault(parsed);
            occurrences[parsed] = occurrence + 1;

            transactions.Add(new BankTransaction
            {
                TutorId = import.TutorId,
                ImportId = import.Id,
                BookingDate = parsed.BookingDate,
                Amount = parsed.Amount,
                Currency = parsed.Currency,
                CounterpartyAccount = parsed.CounterpartyAccount,
                CounterpartyName = Truncate(parsed.CounterpartyName, 300),
                Title = Truncate(parsed.Title, 500),
                TransactionKey = TransactionKeys.For(parsed, occurrence),
            });
        }

        return transactions;
    }

    /// <summary>Only small .csv files are accepted; the content is parsed into typed values and nothing else.</summary>
    private static async Task<byte[]> ReadValidatedAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw ApiException.Validation("file", "Choose a CSV file exported from your bank.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Validation("file", "Only .csv files are supported.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw ApiException.Validation("file", "The file is larger than 1 MB.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        return stream.ToArray();
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
