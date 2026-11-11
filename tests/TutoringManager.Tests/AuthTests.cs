using System.Net;
using System.Net.Http.Json;
using TutoringManager.Api.Auth;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Registered_tutor_is_signed_in()
    {
        var client = await factory.CreateTutorClientAsync("Anna Tutor");

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();

        Assert.Equal("Anna Tutor", me.DisplayName);
        Assert.Equal("Europe/Warsaw", me.TimeZoneId);
    }

    [Fact]
    public async Task Tutor_can_log_out_and_log_back_in()
    {
        var client = await factory.CreateAnonymousClientAsync();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestClients.Password, displayName = "T" });
        await client.RefreshAntiforgeryTokenAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        await client.RefreshAntiforgeryTokenAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestClients.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        var client = await factory.CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "short@example.com", password = "too-short", displayName = "T" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_logins()
    {
        var client = await factory.CreateAnonymousClientAsync();
        var email = $"lock-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestClients.Password, displayName = "T" });
        await client.PostAsync("/api/auth/logout", null);
        await client.RefreshAntiforgeryTokenAsync();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong password!!" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var fifth = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong password!!" });
        Assert.Equal(HttpStatusCode.Locked, fifth.StatusCode);

        var correctPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestClients.Password });
        Assert.Equal(HttpStatusCode.Locked, correctPassword.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_as_wrong_password()
    {
        var client = await factory.CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = TestClients.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_antiforgery_header_is_rejected()
    {
        var client = await factory.CreateTutorClientAsync();
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var response = await client.PostAsJsonAsync("/api/students",
            new { fullName = "Jan Kowalski", email = "jan@example.com", ratePerLesson = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_to_tutor_endpoint_gets_401()
    {
        var client = await factory.CreateAnonymousClientAsync();

        var response = await client.GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
