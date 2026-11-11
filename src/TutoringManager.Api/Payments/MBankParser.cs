using System.Globalization;
using TutoringManager.Api.Students;

namespace TutoringManager.Api.Payments;

/// <summary>
/// mBank "Lista operacji" CSV export: a few lines of account metadata, then a header row
/// (#Data księgowania;#Data operacji;#Opis operacji;#Tytuł;#Nadawca/Odbiorca;#Numer konta;#Kwota;...)
/// and one operation per line. Amounts use a decimal comma and spaces as thousand separators.
/// Columns are found by name, so their order does not matter.
/// </summary>
public class MBankParser : IBankStatementParser
{
    private const string DateColumn = "#Data księgowania";
    private const string TitleColumn = "#Tytuł";
    private const string CounterpartyColumn = "#Nadawca/Odbiorca";
    private const string AccountColumn = "#Numer konta";
    private const string AmountColumn = "#Kwota";

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "dd-MM-yyyy"];

    public string BankFormat => "mBank";

    public bool CanParse(string content) => FindHeader(SplitLines(content)) is not null;

    public IReadOnlyList<ParsedTransaction> Parse(string content)
    {
        var lines = SplitLines(content);
        var headerIndex = FindHeader(lines) ?? throw new FormatException("mBank header row not found.");
        var header = StatementText.SplitLine(lines[headerIndex]);
        int Column(string name) => header.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        var (date, title, counterparty, account, amount) =
            (Column(DateColumn), Column(TitleColumn), Column(CounterpartyColumn), Column(AccountColumn), Column(AmountColumn));

        var transactions = new List<ParsedTransaction>();
        foreach (var line in lines.Skip(headerIndex + 1))
        {
            var fields = StatementText.SplitLine(line);
            if (fields.Count <= Math.Max(date, amount)
                || !DateOnly.TryParseExact(fields[date], DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var bookingDate)
                || !TryParseAmount(fields[amount], out var value, out var currency))
            {
                continue; // blank lines, footers and summaries
            }

            transactions.Add(new ParsedTransaction(
                bookingDate,
                value,
                currency,
                account >= 0 ? BankAccountNumber.Normalize(fields[account]) : "",
                counterparty >= 0 ? CollapseSpaces(fields[counterparty]) : "",
                title >= 0 ? CollapseSpaces(fields[title]) : ""));
        }

        return transactions;
    }

    private static int? FindHeader(string[] lines)
    {
        var index = Array.FindIndex(lines, l => l.Contains(DateColumn, StringComparison.OrdinalIgnoreCase)
            && l.Contains(AmountColumn, StringComparison.OrdinalIgnoreCase)
            && l.Contains(AccountColumn, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : null;
    }

    /// <summary>"1 234,56", "1 234,56 PLN" or "-50,00" → value and currency (PLN when not given).</summary>
    private static bool TryParseAmount(string raw, out decimal value, out string currency)
    {
        currency = "PLN";
        var text = raw.Replace(" ", " ").Trim();
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts[^1].All(char.IsAsciiLetterUpper) && parts[^1].Length == 3)
        {
            currency = parts[^1];
            parts = parts[..^1];
        }

        var number = string.Concat(parts).Replace(',', '.');
        return decimal.TryParse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string[] SplitLines(string content) => content.Split(["\r\n", "\n"], StringSplitOptions.None);

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
