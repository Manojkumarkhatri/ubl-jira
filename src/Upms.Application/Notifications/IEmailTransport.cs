namespace Upms.Application.Notifications;

/// <summary>Hands one email to the company mail server (Phase 4 research R10): MailKit in production, a fake in tests.
/// A failure throws; the dispatcher retries.</summary>
public interface IEmailTransport
{
    /// <summary>False when no mail server is configured: then no email is queued at all.</summary>
    bool IsConfigured { get; }

    Task SendAsync(OutgoingEmail email, CancellationToken ct);
}

/// <summary>An email as it leaves U-PMS: plain text and simple HTML with the same content.</summary>
public sealed record OutgoingEmail(string FromAddress, string FromName, string ToAddress, string ToName, string Subject, string TextBody,
    string HtmlBody);
