using TutoringManager.Api.Payments;

namespace TutoringManager.Tests.Payments;

public class MBankParserTests
{
    private readonly MBankParser _parser = new();

    [Fact]
    public void Windows_1250_file_is_decoded_and_parsed()
    {
        var content = StatementText.Decode(File.ReadAllBytes(Sample("mbank-2027-01-windows1250.csv")));

        Assert.True(_parser.CanParse(content));
        var transactions = _parser.Parse(content);

        Assert.Equal(4, transactions.Count);
        var parent = transactions[1];
        Assert.Equal(new DateOnly(2027, 1, 12), parent.BookingDate);
        Assert.Equal(1200.00m, parent.Amount); // "1 200,00"
        Assert.Equal("EWA WIŚNIEWSKA UL. SŁONECZNA 5/12 30-001 KRAKÓW", parent.CounterpartyName);
        Assert.Equal("Za lekcje Tomka - styczeń", parent.Title);
        Assert.Equal("60102010260000042270201111", parent.CounterpartyAccount);
        Assert.Equal(-150.00m, transactions[2].Amount);
    }

    [Fact]
    public void Utf8_file_is_parsed()
    {
        var content = StatementText.Decode(File.ReadAllBytes(Sample("mbank-2027-01-15-to-02-15-utf8.csv")));

        var transactions = _parser.Parse(content);

        Assert.Equal(4, transactions.Count);
        Assert.Equal(45.50m, transactions[^1].Amount);
        Assert.Equal("PIOTR ZIELIŃSKI", transactions[^1].CounterpartyName);
    }

    [Theory]
    [InlineData("300,00", 300.00)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("300,00 PLN", 300.00)]
    [InlineData("-50,00", -50.00)]
    public void Amounts_with_decimal_comma_and_thousand_separators_are_parsed(string raw, decimal expected)
    {
        var content = $"#Data księgowania;#Tytuł;#Numer konta;#Kwota;\n2027-01-05;\"x\";'';{raw};";

        Assert.Equal(expected, Assert.Single(_parser.Parse(content)).Amount);
    }

    [Fact]
    public void Quoted_fields_may_contain_semicolons()
    {
        var fields = StatementText.SplitLine("2027-01-05;\"Lessons; January\";\"He said \"\"hi\"\"\";300,00");

        Assert.Equal(["2027-01-05", "Lessons; January", "He said \"hi\"", "300,00"], fields);
    }

    [Fact]
    public void Other_files_are_not_recognised()
    {
        Assert.False(_parser.CanParse("Date,Description,Amount\n2027-01-05,Coffee,-3.50"));
    }

    public static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Payments", "Samples", name);
}
