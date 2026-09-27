using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upms.Application.Work;

namespace Upms.Infrastructure.Workers;

/// <summary>Periodically shortens long card rank keys (research R14). Card order never changes.</summary>
internal sealed partial class RankRebalanceWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<RankRebalanceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IRankRebalancer>().RebalanceLongRanksAsync(stoppingToken);
                if (count > 0)
                {
                    LogRebalanced(logger, count);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rebalanced card ranks in {Count} lists.")]
    private static partial void LogRebalanced(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rank rebalancing failed; it will be retried.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
