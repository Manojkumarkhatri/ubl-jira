using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Upms.Application.Work;

namespace Upms.Infrastructure.Workers;

/// <summary>Indexes work items that have no search words yet (Phase 4 research R15): after the upgrade it works through the
/// existing items, 500 at a time, and says when search is complete; afterwards it picks up the odd item made elsewhere.</summary>
internal sealed partial class SearchIndexWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<SearchIndexWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var catchingUp = false;
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                int indexed;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    indexed = await scope.ServiceProvider.GetRequiredService<ISearchIndexBackfill>().IndexPendingAsync(stoppingToken);
                    catchingUp |= indexed == SearchIndexer.BatchSize;
                }
                while (indexed == SearchIndexer.BatchSize && !stoppingToken.IsCancellationRequested);

                if (catchingUp)
                {
                    catchingUp = false;
                    LogComplete(logger);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The search index is complete: every work item can be found.")]
    private static partial void LogComplete(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Indexing work items for search failed; it will be retried.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
