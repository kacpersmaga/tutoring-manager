using System.Net;
using System.Net.Http.Json;
using System.Text;
using TutoringManager.Api.Payments;
using TutoringManager.Api.Students;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests.Payments;

[Collection(ApiCollection.Name)]
public class ReportTests(ApiFactory factory)
{
    // The fake clock is at 2027-01-11, so November and December 2026 are in the past and lessons can be completed.
    private const string Account = "27114020040000300201355387";

    [Fact]
    public async Task Prepayment_shows_as_positive_balance_and_is_used_up_later()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 100m);
        await CompletedLessonsAsync(tutor, student.Id, "2026-11-10", "2026-11-20", "2026-12-01", "2026-12-08");
        await PaymentsAsync(tutor, ("2026-11-02", 400m));

        var november = await StudentRowAsync(tutor, "2026-11", student.Id);
        Assert.Equal((0m, 200m, 400m, 200m), (november.OpeningBalance, november.DueInMonth, november.PaidInMonth, november.ClosingBalance));

        var december = await StudentRowAsync(tutor, "2026-12", student.Id);
        Assert.Equal((200m, 200m, 0m, 0m), (december.OpeningBalance, december.DueInMonth, december.PaidInMonth, december.ClosingBalance));
    }

    [Fact]
    public async Task Late_payment_settles_the_previous_month()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 100m);
        await CompletedLessonsAsync(tutor, student.Id, "2026-11-10", "2026-11-20");
        await PaymentsAsync(tutor, ("2026-12-05", 200m));

        Assert.Equal(-200m, (await StudentRowAsync(tutor, "2026-11", student.Id)).ClosingBalance);
        var december = await StudentRowAsync(tutor, "2026-12", student.Id);
        Assert.Equal((-200m, 0m, 200m, 0m), (december.OpeningBalance, december.DueInMonth, december.PaidInMonth, december.ClosingBalance));
    }

    [Fact]
    public async Task One_transfer_can_cover_two_months()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 120m);
        await CompletedLessonsAsync(tutor, student.Id, "2026-11-10", "2026-11-17", "2026-12-01", "2026-12-08");
        await PaymentsAsync(tutor, ("2026-12-28", 480m));

        Assert.Equal(-240m, (await StudentRowAsync(tutor, "2026-11", student.Id)).ClosingBalance);
        Assert.Equal(0m, (await StudentRowAsync(tutor, "2026-12", student.Id)).ClosingBalance);
    }

    [Fact]
    public async Task Overpayment_is_carried_forward()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 100m);
        await CompletedLessonsAsync(tutor, student.Id, "2026-12-01", "2026-12-08");
        await PaymentsAsync(tutor, ("2026-12-02", 500m));

        Assert.Equal(300m, (await StudentRowAsync(tutor, "2026-12", student.Id)).ClosingBalance);
        Assert.Equal(300m, (await StudentRowAsync(tutor, "2027-01", student.Id)).OpeningBalance);
        var details = await (await tutor.GetAsync($"/api/students/{student.Id}")).ReadAsync<StudentDetails>();
        Assert.Equal(300m, details.Balance);
    }

    [Fact]
    public async Task Changing_the_rate_does_not_change_history()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 100m);
        await CompletedLessonsAsync(tutor, student.Id, "2026-11-10");

        await tutor.PutAsJsonAsync($"/api/students/{student.Id}",
            new { fullName = student.FullName, email = student.Email, ratePerLesson = 150m, isActive = true });
        await CompletedLessonsAsync(tutor, student.Id, "2026-12-01");

        Assert.Equal(100m, (await StudentRowAsync(tutor, "2026-11", student.Id)).DueInMonth);
        Assert.Equal(150m, (await StudentRowAsync(tutor, "2026-12", student.Id)).DueInMonth);
        Assert.Equal(-250m, (await StudentRowAsync(tutor, "2026-12", student.Id)).ClosingBalance);
    }

    [Fact]
    public async Task Scheduled_and_cancelled_lessons_are_not_due()
    {
        var (tutor, student) = await CreateTutorAndStudentAsync(rate: 100m);
        await CompletedLessonsAsync(tutor, student.Id, "2027-01-04");
        var cancelled = await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-05", "10:00");
        await tutor.PostAsync($"/api/lessons/{cancelled.Id}/cancel", null);
        await LessonTests.CreateLessonAsync(tutor, student.Id, "2027-01-20", "10:00");

        var january = await StudentRowAsync(tutor, "2027-01", student.Id);

        Assert.Equal(1, january.LessonsCompleted);
        Assert.Equal(100m, january.DueInMonth);
    }

    [Fact]
    public async Task Invalid_month_is_rejected()
    {
        var tutor = await factory.CreateTutorClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await tutor.GetAsync("/api/payments/report?month=January")).StatusCode);
    }

    private async Task<(HttpClient Tutor, StudentDetails Student)> CreateTutorAndStudentAsync(decimal rate)
    {
        var tutor = await factory.CreateTutorClientAsync();
        var student = await (await tutor.PostAsJsonAsync("/api/students",
            new { fullName = "Anna Nowak", email = "anna@example.com", ratePerLesson = rate, bankAccounts = new[] { Account } }))
            .ReadAsync<StudentDetails>();
        return (tutor, student);
    }

    private static async Task CompletedLessonsAsync(HttpClient tutor, Guid studentId, params string[] dates)
    {
        foreach (var date in dates)
        {
            var lesson = await LessonTests.CreateLessonAsync(tutor, studentId, date, "16:00");
            (await tutor.PostAsync($"/api/lessons/{lesson.Id}/complete", null)).EnsureSuccessStatusCode();
        }
    }

    private static async Task PaymentsAsync(HttpClient tutor, params (string Date, decimal Amount)[] payments)
    {
        var csv = new StringBuilder("#Data księgowania;#Data operacji;#Opis operacji;#Tytuł;#Nadawca/Odbiorca;#Numer konta;#Kwota;#Saldo po operacji;\n");
        foreach (var (date, amount) in payments)
        {
            csv.AppendLine($"{date};{date};PRZELEW;\"Lekcje\";\"ANNA NOWAK\";'{Account}';{amount:0.00};0,00;".Replace('.', ','));
        }

        (await ImportTests.UploadAsync(tutor, $"{Guid.NewGuid():N}.csv", Encoding.UTF8.GetBytes(csv.ToString()))).EnsureSuccessStatusCode();
    }

    private static async Task<StudentMonthReport> StudentRowAsync(HttpClient tutor, string month, Guid studentId)
    {
        var report = await (await tutor.GetAsync($"/api/payments/report?month={month}")).ReadAsync<MonthlyReport>();
        return report.Students.Single(s => s.StudentId == studentId);
    }
}
