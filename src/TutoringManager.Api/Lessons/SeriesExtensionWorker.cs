namespace TutoringManager.Api.Lessons;

/// <summary>Once a day (and at startup) generates the next lessons of every series, keeping 12 weeks ahead.</summary>
public class SeriesExtensionWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<SeriesExtensionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var series = scope.ServiceProvider.GetRequiredService<SeriesService>();
                var created = await series.ExtendAllAsync(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
                logger.LogInformation("Series extension created {Count} lessons", created);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Series extension failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
