using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TutoringManager.Api.Auth;
using TutoringManager.Api.Demo;
using TutoringManager.Api.Lessons;
using TutoringManager.Api.Payments;
using TutoringManager.Api.Students;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class DemoAndSecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Demo_account_is_created_on_startup_with_realistic_data()
    {
        var (demo, client) = await StartDemoAsync();
        using var _ = demo;

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<MeResponse>();
        Assert.True(me.IsDemo);

        var students = await (await client.GetAsync("/api/students")).ReadAsync<List<StudentSummary>>();
        Assert.Equal(DemoData.Students.Length, students.Count);

        var week = await (await client.GetAsync("/api/lessons?from=2027-01-11&to=2027-01-17")).ReadAsync<List<LessonResponse>>();
        Assert.Equal(DemoData.Students.Length, week.Count);
        var past = await (await client.GetAsync("/api/lessons?from=2027-01-01&to=2027-01-10")).ReadAsync<List<LessonResponse>>();
        Assert.Contains(past, l => l.Status == LessonStatus.Completed && l.Note is not null);

        var unmatched = await (await client.GetAsync("/api/payments/unmatched")).ReadAsync<List<TransactionResponse>>();
        Assert.Equal(2, unmatched.Count);
    }

    [Fact]
    public async Task Demo_can_be_reset_by_the_demo_account_only()
    {
        var (demo, client) = await StartDemoAsync();
        using var _ = demo;
        var student = (await (await client.GetAsync("/api/students")).ReadAsync<List<StudentSummary>>()).First();
        await client.DeleteAsync($"/api/students/{student.Id}");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/demo/reset", null)).StatusCode);
        Assert.Equal(DemoData.Students.Length,
            (await (await client.GetAsync("/api/students")).ReadAsync<List<StudentSummary>>()).Count);

        var regularTutor = await factory.CreateTutorClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await regularTutor.PostAsync("/api/demo/reset", null)).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Unknown_api_path_is_404_not_the_spa()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(WebApplicationFactory<Program> Demo, HttpClient Client)> StartDemoAsync()
    {
        var email = $"demo-{Guid.NewGuid():N}@example.com";
        const string password = "demo password for tests";
        var demo = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Demo:Enabled", "true")
            .UseSetting("Demo:Email", email)
            .UseSetting("Demo:Password", password));

        var client = demo.CreateClient();
        await client.RefreshAntiforgeryTokenAsync();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).EnsureSuccessStatusCode();
        await client.RefreshAntiforgeryTokenAsync();
        return (demo, client);
    }
}
