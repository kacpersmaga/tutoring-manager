using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TutoringManager.Api.Emails;
using Testcontainers.PostgreSql;

namespace TutoringManager.Tests.Infrastructure;

/// <summary>
/// Runs the real API against a real PostgreSQL in a container. One database is shared by all tests;
/// tests stay independent because each one registers its own tutor.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2027, 1, 11, 9, 0, 0, TimeSpan.Zero));
    public FakeEmailSender Emails { get; } = new();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        // All test requests come from the same (unknown) IP; rate limiting has its own tests.
        builder.UseSetting("RateLimits:AuthPerMinute", "100000");
        builder.UseSetting("RateLimits:AttendancePerMinute", "100000");
        // Background workers are driven explicitly by tests instead of on a timer.
        builder.UseSetting("Workers:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
