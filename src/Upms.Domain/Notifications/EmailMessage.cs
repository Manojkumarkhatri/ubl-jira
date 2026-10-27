namespace Upms.Domain.Notifications;

/// <summary>An email waiting to be sent about one notification (Phase 4 FR-049–FR-052, research R10): written in the same
/// transaction as the notification, sent later by a background worker with its text built at that moment, retried after
/// 1, 2, 5, 10 and 20 minutes and then every 30 minutes, and given up 24 hours after it was created.</summary>
public sealed class EmailMessage
{
    public const int ErrorMaxLength = 500;

    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(24);

    private static readonly int[] FirstRetries = [1, 2, 5, 10, 20];
    private const int LaterRetry = 30;

    private EmailMessage()
    {
    }

    public long Id { get; private set; }

    public long NotificationId { get; private set; }

    /// <summary>Set when the email is created with its notification, so both are saved together.</summary>
    public Notification? Notification { get; private set; }

    /// <summary>The address is read from the account when sending.</summary>
    public Guid RecipientId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Sends tried, successful or not.</summary>
    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Set while a worker instance is sending it.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? GivenUpAt { get; private set; }

    /// <summary>The recipient was deactivated, or no longer wants this email, before it was sent.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>The last failure, never the message's content.</summary>
    public string? LastError { get; private set; }

    public EmailState State => SentAt is not null ? EmailState.Sent
        : GivenUpAt is not null ? EmailState.GivenUp
        : CancelledAt is not null ? EmailState.Cancelled
        : EmailState.Pending;

    public static EmailMessage For(Notification notification, DateTimeOffset now) => new()
    {
        Notification = notification,
        RecipientId = notification.RecipientId,
        CreatedAt = now,
        NextAttemptAt = now,
    };

    /// <summary>Held by one worker instance until it is sent, fails or the lease ends.</summary>
    public void Claim(DateTimeOffset now, TimeSpan lease) => LockedUntil = now + lease;

    public void Sent(DateTimeOffset now)
    {
        Attempts++;
        SentAt = now;
        LockedUntil = null;
    }

    /// <summary>Schedules the next attempt, or gives up 24 hours after creation; true when given up.</summary>
    public bool Failed(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
        LockedUntil = null;
        if (now >= CreatedAt + GiveUpAfter)
        {
            GivenUpAt = now;
            return true;
        }

        NextAttemptAt = now.AddMinutes(Attempts <= FirstRetries.Length ? FirstRetries[Attempts - 1] : LaterRetry);
        return false;
    }

    public void Cancel(DateTimeOffset now)
    {
        CancelledAt = now;
        LockedUntil = null;
    }
}

public enum EmailState
{
    Pending,
    Sent,
    GivenUp,
    Cancelled,
}
