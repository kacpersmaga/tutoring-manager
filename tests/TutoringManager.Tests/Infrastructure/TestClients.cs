using System.Net.Http.Json;

namespace TutoringManager.Tests.Infrastructure;

public static class TestClients
{
    public const string Password = "correct horse battery";

    /// <summary>Registers a fresh tutor and returns a client that is signed in and sends the antiforgery header.</summary>
    public static async Task<HttpClient> CreateTutorClientAsync(this ApiFactory factory, string? displayName = null)
    {
        var client = await factory.CreateAnonymousClientAsync();
        var email = $"tutor-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, displayName = displayName ?? "Test Tutor" });
        response.EnsureSuccessStatusCode();

        // The antiforgery token is bound to the signed-in user, so it has to be refreshed after signing in.
        await client.RefreshAntiforgeryTokenAsync();
        return client;
    }

    public static async Task<HttpClient> CreateAnonymousClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        await client.RefreshAntiforgeryTokenAsync();
        return client;
    }

    public static async Task RefreshAntiforgeryTokenAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/antiforgery");
        response.EnsureSuccessStatusCode();

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN="));
        var token = cookie.Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions.Default))!;
    }
}
