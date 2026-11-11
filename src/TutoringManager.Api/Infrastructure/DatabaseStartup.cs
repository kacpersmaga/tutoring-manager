using Microsoft.EntityFrameworkCore;

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
}
