using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;

namespace Upms.Domain.Tests.Notifications;

/// <summary>Which notifications are also emailed, and how an email is retried (Phase 4 FR-049, FR-052, research R10,
/// R11).</summary>
public sealed class EmailRulesTests
{
    private static readonly Guid Bilal = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(NotificationKind.Assigned, false, true, true)]
    [InlineData(NotificationKind.Mentioned, false, true, true)]
    [InlineData(NotificationKind.ApprovalRequested, false, true, true)]
    [InlineData(NotificationKind.GateDecided, false, true, true)]
    [InlineData(NotificationKind.UploadBlocked, false, true, true)]
    [InlineData(NotificationKind.Commented, false, false, true)]
    [InlineData(NotificationKind.StatusChanged, false, false, true)]
    public void P4_US5_Each_choice_emails_its_kinds(NotificationKind kind, bool none, bool aimedAtMe, bool all)
    {
        Assert.Equal((none, aimedAtMe, all),
            (RecipientRules.IsEmailed(kind, EmailChoice.None), RecipientRules.IsEmailed(kind, EmailChoice.AimedAtMe),
                RecipientRules.IsEmailed(kind, EmailChoice.All)));
    }

    [Fact]
    public void A_new_email_is_due_at_once()
    {
        var email = EmailMessage.For(Notification(), T0);

        Assert.Equal((Bilal, T0, T0, 0, EmailState.Pending), (email.RecipientId, email.CreatedAt, email.NextAttemptAt, email.Attempts, email.State));
    }

    [Fact]
    public void P4_US5_AS8_Failures_are_retried_after_1_2_5_10_20_and_then_every_30_minutes()
    {
        var email = EmailMessage.For(Notification(), T0);
        var now = T0;
        var waits = new List<double>();
        for (var i = 0; i < 8; i++)
        {
            email.Claim(now, TimeSpan.FromMinutes(2));
            email.Failed("Connection refused", now);
            waits.Add((email.NextAttemptAt - now).TotalMinutes);
            now = email.NextAttemptAt;
        }

        Assert.Equal([1, 2, 5, 10, 20, 30, 30, 30], waits);
        Assert.Equal((8, "Connection refused", (DateTimeOffset?)null), (email.Attempts, email.LastError, email.LockedUntil));
    }

    [Fact]
    public void P4_US5_AS8_A_failure_24_hours_after_creation_gives_the_email_up()
    {
        var email = EmailMessage.For(Notification(), T0);

        email.Claim(T0.AddHours(23).AddMinutes(59), TimeSpan.FromMinutes(2));
        Assert.False(email.Failed("Timeout", T0.AddHours(23).AddMinutes(59)));
        Assert.Equal(EmailState.Pending, email.State);

        email.Claim(T0.AddHours(24), TimeSpan.FromMinutes(2));
        Assert.True(email.Failed("Timeout", T0.AddHours(24)));

        Assert.Equal((EmailState.GivenUp, (DateTimeOffset?)T0.AddHours(24)), (email.State, email.GivenUpAt));
    }

    [Fact]
    public void Errors_are_kept_short()
    {
        var email = EmailMessage.For(Notification(), T0);

        email.Failed(new string('x', 900), T0);

        Assert.Equal(EmailMessage.ErrorMaxLength, email.LastError!.Length);
    }

    [Fact]
    public void Sent_and_cancelled_emails_are_finished()
    {
        var sent = EmailMessage.For(Notification(), T0);
        sent.Claim(T0, TimeSpan.FromMinutes(2));
        sent.Sent(T0.AddSeconds(3));
        var cancelled = EmailMessage.For(Notification(), T0);
        cancelled.Cancel(T0.AddSeconds(3));

        Assert.Equal((EmailState.Sent, (DateTimeOffset?)T0.AddSeconds(3), (DateTimeOffset?)null), (sent.State, sent.SentAt, sent.LockedUntil));
        Assert.Equal((EmailState.Cancelled, (DateTimeOffset?)T0.AddSeconds(3)), (cancelled.State, cancelled.CancelledAt));
    }

    private static Notification Notification() =>
        Upms.Domain.Notifications.Notification.Create(Bilal, NotificationKind.Assigned, 1, 2, Guid.NewGuid(), null, T0);
}
