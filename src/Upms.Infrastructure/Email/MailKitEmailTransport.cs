using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MimeKit;
using Upms.Application.Notifications;

namespace Upms.Infrastructure.Email;

/// <summary>The company mail server (Phase 4 research R10, R23): <c>Email:Smtp:Host</c>, <c>Port</c>, <c>Security</c>
/// (<c>Auto</c>, <c>StartTls</c>, <c>SslOnConnect</c> or <c>None</c>) and, when it asks for them, <c>UserName</c> and
/// <c>Password</c> (from a secret store, never committed). Without a host, email is off.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>How long one send may take before it counts as failed and is retried.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>Sends one email with MailKit (research R10), a connection per email: the worker sends at most 20 every 15
/// seconds, so pooling connections is not worth its failure modes.</summary>
internal sealed class MailKitEmailTransport(IOptions<SmtpOptions> options) : IEmailTransport, IHealthCheck
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Host);

    public async Task SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(new MailboxAddress(email.ToName, email.ToAddress));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { TextBody = email.TextBody, HtmlBody = email.HtmlBody }.ToMessageBody();

        using var client = await ConnectAsync(ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    /// <summary>Reachable and accepting the credentials: readiness is only degraded otherwise, as emails wait in the outbox.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = await ConnectAsync(cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded("The mail server cannot be reached; emails wait and are retried.", e);
        }
    }

    private async Task<SmtpClient> ConnectAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var client = new SmtpClient { Timeout = (int)settings.Timeout.TotalMilliseconds };
        try
        {
            await client.ConnectAsync(settings.Host!, settings.Port, settings.Security, ct);
            if (!string.IsNullOrEmpty(settings.UserName))
            {
                await client.AuthenticateAsync(settings.UserName, settings.Password ?? "", ct);
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
