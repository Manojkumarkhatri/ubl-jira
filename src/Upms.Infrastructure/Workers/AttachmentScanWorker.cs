using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upms.Application.Work;
using Upms.Infrastructure.Malware;

namespace Upms.Infrastructure.Workers;

/// <summary>Checks new uploads for malware every 10 seconds (Phase 4 FR-025, research R14).</summary>
internal sealed partial class AttachmentScanWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ClamAvOptions> options,
    TimeProvider time,
    ILogger<AttachmentScanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Host))
        {
            LogNoScanner(logger);
        }

        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAttachmentScanner>().ScanDueAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No malware scanner is configured (Malware:ClamAv:Host): uploads will stay being checked and cannot be downloaded.")]
    private static partial void LogNoScanner(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking uploads for malware failed; it will be retried.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
