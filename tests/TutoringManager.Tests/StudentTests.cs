using System.Net;
using System.Net.Http.Json;
using TutoringManager.Api.Students;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class StudentTests(ApiFactory factory)
{
    // A valid Polish account number (correct IBAN checksum), written the way people paste it.
    public const string ValidAccount = "PL61 1090 1014 0000 0712 1981 2874";
    public const string ValidAccountNormalized = "61109010140000071219812874";
    public const string OtherValidAccount = "27 1140 2004 0000 3002 0135 5387";

    [Fact]
    public async Task Tutor_can_create_update_and_delete_a_student()
    {
        var client = await factory.CreateTutorClientAsync();

        var created = await (await client.PostAsJsonAsync("/api/students", new
        {
            fullName = "Jan Kowalski",
            email = "jan@example.com",
            ratePerLesson = 120.50m,
            bankAccounts = new[] { ValidAccount },
        })).ReadAsync<StudentDetails>();

        Assert.Equal(120.50m, created.RatePerLesson);
        Assert.Equal(ValidAccountNormalized, Assert.Single(created.BankAccounts).AccountNumber);

        var updated = await (await client.PutAsJsonAsync($"/api/students/{created.Id}", new
        {
            fullName = "Jan Nowak",
            email = "jan@example.com",
            ratePerLesson = 130m,
            isActive = false,
        })).ReadAsync<StudentDetails>();
        Assert.Equal("Jan Nowak", updated.FullName);
        Assert.False(updated.IsActive);

        var activeOnly = await (await client.GetAsync("/api/students")).ReadAsync<List<StudentSummary>>();
        Assert.Empty(activeOnly);
        var all = await (await client.GetAsync("/api/students?includeInactive=true")).ReadAsync<List<StudentSummary>>();
        Assert.Single(all);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/students/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/students/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("", "jan@example.com", 100)]
    [InlineData("Jan", "not-an-email", 100)]
    [InlineData("Jan", "jan@example.com", -1)]
    public async Task Invalid_student_is_rejected(string fullName, string email, decimal rate)
    {
        var client = await factory.CreateTutorClientAsync();

        var response = await client.PostAsJsonAsync("/api/students", new { fullName, email, ratePerLesson = rate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Account_number_with_wrong_checksum_is_rejected()
    {
        var client = await factory.CreateTutorClientAsync();
        var student = await CreateStudentAsync(client);

        var response = await client.PostAsJsonAsync($"/api/students/{student.Id}/accounts",
            new { accountNumber = "61 1090 1014 0000 0712 1981 2875" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Same_account_cannot_belong_to_two_students_of_one_tutor()
    {
        var client = await factory.CreateTutorClientAsync();
        var first = await CreateStudentAsync(client);
        var second = await CreateStudentAsync(client);
        await client.PostAsJsonAsync($"/api/students/{first.Id}/accounts", new { accountNumber = OtherValidAccount });

        var response = await client.PostAsJsonAsync($"/api/students/{second.Id}/accounts", new { accountNumber = OtherValidAccount });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Two_tutors_can_have_a_student_paying_from_the_same_account()
    {
        var tutorA = await factory.CreateTutorClientAsync();
        var tutorB = await factory.CreateTutorClientAsync();

        var a = await tutorA.PostAsJsonAsync("/api/students", new { fullName = "Ola", email = "ola@example.com", ratePerLesson = 100m, bankAccounts = new[] { OtherValidAccount } });
        var b = await tutorB.PostAsJsonAsync("/api/students", new { fullName = "Ola", email = "ola@example.com", ratePerLesson = 100m, bankAccounts = new[] { OtherValidAccount } });

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
    }

    [Fact]
    public async Task Tutor_cannot_see_or_change_another_tutors_student()
    {
        var owner = await factory.CreateTutorClientAsync();
        var intruder = await factory.CreateTutorClientAsync();
        var student = await CreateStudentAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync($"/api/students/{student.Id}",
            new { fullName = "Hacked", email = "h@example.com", ratePerLesson = 1m, isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsJsonAsync($"/api/students/{student.Id}/accounts",
            new { accountNumber = ValidAccount })).StatusCode);
        Assert.Empty(await (await intruder.GetAsync("/api/students")).ReadAsync<List<StudentSummary>>());

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/students/{student.Id}")).StatusCode);
    }

    public static async Task<StudentDetails> CreateStudentAsync(HttpClient client, decimal rate = 100m, string? name = null) =>
        await (await client.PostAsJsonAsync("/api/students", new
        {
            fullName = name ?? "Student " + Guid.NewGuid().ToString("N")[..6],
            email = "student@example.com",
            ratePerLesson = rate,
        })).ReadAsync<StudentDetails>();
}
