using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upms.Application.Notifications;
using Upms.Infrastructure.Email;

namespace Upms.Infrastructure.Workers;

/// <summary>Sends queued emails every 15 seconds (Phase 4 FR-052, research R10), so no action ever waits for the mail
/// server.</summary>
internal sealed partial class EmailDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SmtpOptions> smtp,
    IOptions<EmailOptions> email,
    TimeProvider time,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(smtp.Value.Host))
        {
            LogEmailOff(logger);
            return;
        }

        if (string.IsNullOrWhiteSpace(email.Value.FromAddress) || string.IsNullOrWhiteSpace(email.Value.PublicBaseUrl))
        {
            LogIncomplete(logger);
        }

        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                int sent;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    sent = await scope.ServiceProvider.GetRequiredService<IEmailDispatcher>().SendDueAsync(stoppingToken);
                }
                while (sent > 0 && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No mail server is configured (Email:Smtp:Host): no emails are sent or queued.")]
    private static partial void LogEmailOff(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Email:FromAddress or App:PublicBaseUrl is not set: emails may be refused or carry links that do not open.")]
    private static partial void LogIncomplete(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending emails failed; it will be retried.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
