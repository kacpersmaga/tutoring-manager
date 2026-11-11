using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TutoringManager.Api.Payments;
using TutoringManager.Api.Students;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests.Payments;

[Collection(ApiCollection.Name)]
public class ImportTests(ApiFactory factory)
{
    private const string January = "mbank-2027-01-windows1250.csv";
    private const string MidJanuaryToMidFebruary = "mbank-2027-01-15-to-02-15-utf8.csv";
    private const string AnnaAccount = "27114020040000300201355387";
    private const string ParentAccount = "60102010260000042270201111";

    [Fact]
    public async Task Import_keeps_only_income_and_matches_by_sender_account()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var anna = await CreateStudentWithAccountAsync(tutor, "Anna Nowak", AnnaAccount);

        var result = await (await UploadAsync(tutor, January)).ReadAsync<ImportResult>();

        Assert.Equal("mBank", result.BankFormat);
        Assert.Equal(3, result.Imported); // the card payment is not income
        Assert.Equal(2, result.Matched);
        Assert.Equal(1, result.Unmatched);
        var payments = await (await tutor.GetAsync($"/api/students/{anna.Id}/payments")).ReadAsync<List<TransactionResponse>>();
        Assert.Equal(600m, payments.Sum(p => p.Amount));
        Assert.All(payments, p => Assert.Equal(MatchedBy.Account, p.MatchedBy));
    }

    [Fact]
    public async Task Same_file_twice_is_rejected()
    {
        var tutor = await factory.CreateTutorClientAsync();
        await UploadAsync(tutor, January);

        var second = await UploadAsync(tutor, January);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Overlapping_statements_do_not_duplicate_transfers()
    {
        var tutor = await factory.CreateTutorClientAsync();
        await UploadAsync(tutor, January);

        var result = await (await UploadAsync(tutor, MidJanuaryToMidFebruary)).ReadAsync<ImportResult>();

        Assert.Equal(4, result.IncomingTransactions);
        Assert.Equal(1, result.SkippedAsDuplicates); // 20 January is in both files
        Assert.Equal(3, result.Imported);
    }

    [Fact]
    public async Task Identical_transfers_on_the_same_day_in_one_file_are_both_kept()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var line = $"2027-03-02;2027-03-02;PRZELEW;\"Lekcje\";\"ANNA\";'{AnnaAccount}';150,00;0,00;";
        var csv = $"#Data księgowania;#Data operacji;#Opis operacji;#Tytuł;#Nadawca/Odbiorca;#Numer konta;#Kwota;#Saldo po operacji;\n{line}\n{line}\n";

        var result = await (await UploadAsync(tutor, "twice.csv", System.Text.Encoding.UTF8.GetBytes(csv))).ReadAsync<ImportResult>();

        Assert.Equal(2, result.Imported);
    }

    [Fact]
    public async Task Transfer_from_a_parent_is_assigned_by_hand_and_the_account_is_remembered()
    {
        var tutor = await factory.CreateTutorClientAsync();
        var tomek = await StudentTests.CreateStudentAsync(tutor, name: "Tomek Wiśniewski");
        await UploadAsync(tutor, January);
        var fromParent = Assert.Single(await (await tutor.GetAsync("/api/payments/unmatched")).ReadAsync<List<TransactionResponse>>(),
            t => t.CounterpartyAccount == ParentAccount);

        var assigned = await (await tutor.PostAsJsonAsync($"/api/payments/transactions/{fromParent.Id}/assign",
            new { studentId = tomek.Id, rememberAccount = true })).ReadAsync<TransactionResponse>();
        Assert.Equal(MatchedBy.Manual, assigned.MatchedBy);
        Assert.Equal("Tomek Wiśniewski", assigned.StudentName);

        var student = await (await tutor.GetAsync($"/api/students/{tomek.Id}")).ReadAsync<StudentDetails>();
        Assert.Contains(student.BankAccounts, a => a.AccountNumber == ParentAccount);

        // The February statement now matches the parent's transfer automatically.
        await UploadAsync(tutor, MidJanuaryToMidFebruary);
        var payments = await (await tutor.GetAsync($"/api/students/{tomek.Id}/payments")).ReadAsync<List<TransactionResponse>>();
        Assert.Equal(2, payments.Count);
        Assert.Contains(payments, p => p.BookingDate == new DateOnly(2027, 2, 10) && p.MatchedBy == MatchedBy.Account);
    }

    [Fact]
    public async Task Only_small_csv_files_in_a_known_format_are_accepted()
    {
        var tutor = await factory.CreateTutorClientAsync();

        var notCsv = await UploadAsync(tutor, "statement.xlsx", [1, 2, 3]);
        var tooBig = await UploadAsync(tutor, "big.csv", new byte[ImportService.MaxFileBytes + 1]);
        var unknown = await UploadAsync(tutor, "other.csv", "Date,Amount\n2027-01-01,5.00"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, notCsv.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Tutor_cannot_see_or_assign_another_tutors_transactions()
    {
        var owner = await factory.CreateTutorClientAsync();
        var intruder = await factory.CreateTutorClientAsync();
        var intrudersStudent = await StudentTests.CreateStudentAsync(intruder);
        await UploadAsync(owner, January);
        var transaction = (await (await owner.GetAsync("/api/payments/unmatched")).ReadAsync<List<TransactionResponse>>()).First();

        Assert.Empty(await (await intruder.GetAsync("/api/payments/unmatched")).ReadAsync<List<TransactionResponse>>());
        var assign = await intruder.PostAsJsonAsync($"/api/payments/transactions/{transaction.Id}/assign",
            new { studentId = intrudersStudent.Id, rememberAccount = false });
        Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);

        // The same file is a different import for a different tutor.
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(intruder, January)).StatusCode);
    }

    private static async Task<StudentDetails> CreateStudentWithAccountAsync(HttpClient tutor, string name, string account) =>
        await (await tutor.PostAsJsonAsync("/api/students",
            new { fullName = name, email = "s@example.com", ratePerLesson = 150m, bankAccounts = new[] { account } }))
            .ReadAsync<StudentDetails>();

    public static Task<HttpResponseMessage> UploadAsync(HttpClient client, string sampleName) =>
        UploadAsync(client, sampleName, File.ReadAllBytes(MBankParserTests.Sample(sampleName)));

    public static Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return client.PostAsync("/api/payments/imports", content);
    }
}
