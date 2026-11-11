using System.Security.Cryptography;
using System.Text;

namespace TutoringManager.Api.Payments;

public record ParsedTransaction(
    DateOnly BookingDate,
    decimal Amount,
    string Currency,
    string CounterpartyAccount,
    string CounterpartyName,
    string Title);

/// <summary>One implementation per bank. Parsers only turn text into typed values; nothing is executed.</summary>
public interface IBankStatementParser
{
    string BankFormat { get; }

    bool CanParse(string content);

    IReadOnlyList<ParsedTransaction> Parse(string content);
}

public static class StatementText
{
    static StatementText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Polish banks export either UTF-8 or Windows-1250. Valid UTF-8 wins; anything else is read as Windows-1250.</summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return text.TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1250).GetString(bytes);
        }
    }

    /// <summary>Splits a semicolon-separated line, honouring double quotes ("a;b" is one field, "" is a quote).</summary>
    public static List<string> SplitLine(string line, char separator = ';')
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == separator && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }
}

public static class TransactionKeys
{
    /// <summary>
    /// Hash of date, amount, account and title, plus the position among identical transfers in the file:
    /// two genuine identical payments on one day stay two, while the same payment in two overlapping
    /// statements gets the same key.
    /// </summary>
    public static string For(ParsedTransaction transaction, int occurrence)
    {
        var source = string.Join('|',
            transaction.BookingDate.ToString("yyyy-MM-dd"),
            transaction.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            transaction.CounterpartyAccount,
            transaction.Title.Trim().ToUpperInvariant(),
            occurrence);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
