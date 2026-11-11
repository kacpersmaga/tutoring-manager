using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TutoringManager.Api.Demo;

namespace TutoringManager.Api.Infrastructure;

public static class DatabaseStartup
{
    // Migrating on startup is the simplest option for a single instance run locally (see README, Design decisions).
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>In local demo mode: creates the demo account on first start and prints where everything is.</summary>
    public static async Task SeedDemoAsync(this WebApplication app)
    {
        var demo = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
        if (!demo.Enabled)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().EnsureSeededAsync();

        var publicUrl = app.Configuration["Email:PublicBaseUrl"] ?? "http://localhost:8080";
        // Printed once the server listens, so it is the last thing in the console.
        app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation(
            """

              Tutoring Manager is running:  {Url}
              Demo account:                 {Email} / {Password}
              Outgoing email (Mailpit):     http://localhost:8025

            """,
            publicUrl, demo.Email, demo.Password));
    }
}
