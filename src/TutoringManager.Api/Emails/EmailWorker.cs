namespace TutoringManager.Api.Emails;

/// <summary>Every 30 seconds sends what is due. A full batch means more may be waiting, so it loops right away.</summary>
public class EmailWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        do
        {
            try
            {
                int claimed;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
                    claimed = await dispatcher.ProcessBatchAsync(stoppingToken);
                }
                while (claimed == EmailDispatcher.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Email dispatch failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
