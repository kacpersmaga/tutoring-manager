using System.Numerics;

namespace TutoringManager.Api.Students;

/// <summary>
/// Polish bank account numbers (NRB): 26 digits, optionally written as an IBAN with "PL" and spaces.
/// </summary>
public static class BankAccountNumber
{
    /// <summary>Strips "PL", spaces, dashes and apostrophes, so the same account always compares equal.</summary>
    public static string Normalize(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        if (value.StartsWith("PL"))
        {
            value = value[2..];
        }

        return new string(value.Where(char.IsAsciiDigit).ToArray());
    }

    /// <summary>Checks the length and the IBAN mod-97 checksum, which catches typos.</summary>
    public static bool IsValid(string normalized)
    {
        if (normalized.Length != 26 || !normalized.All(char.IsAsciiDigit))
        {
            return false;
        }

        // IBAN check: move the country code (P=25, L=21) and the check digits to the end, then take mod 97.
        var rearranged = normalized[2..] + "2521" + normalized[..2];
        return BigInteger.Parse(rearranged) % 97 == 1;
    }
}
