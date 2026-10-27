using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upms.Application.Notifications;

namespace Upms.Infrastructure.Workers;

/// <summary>Deletes notifications older than 90 days, once a day (Phase 4 FR-021, research R9).</summary>
internal sealed partial class NotificationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<NotificationCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<INotificationCleanup>().RunAsync(stoppingToken);
                if (count > 0)
                {
                    LogDeleted(logger, count);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} notifications older than 90 days.")]
    private static partial void LogDeleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting old notifications failed; it will be retried tomorrow.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
