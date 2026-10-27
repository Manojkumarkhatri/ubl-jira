using Upms.Domain.Common;
using Upms.Domain.Notifications;

namespace Upms.Domain.Tests.Notifications;

/// <summary>Who hears about an action, and why (Phase 4 FR-015–FR-017, research R8, R11).</summary>
public sealed class NotificationRulesTests
{
    private static readonly Guid Amina = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Bilal = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Omar = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_actor_is_never_a_recipient()
    {
        var recipients = RecipientRules.Resolve([(NotificationKind.Commented, [Amina, Bilal, Omar])], actorId: Amina);

        Assert.Equal([Bilal, Omar], recipients.Keys.Order());
    }

    [Fact]
    public void A_mention_comes_before_an_assignment_which_comes_before_a_watch()
    {
        var recipients = RecipientRules.Resolve(
        [
            (NotificationKind.Commented, [Bilal, Omar, Amina]),
            (NotificationKind.Assigned, [Omar, Amina]),
            (NotificationKind.Mentioned, [Amina]),
        ], actorId: null);

        Assert.Equal(NotificationKind.Mentioned, recipients[Amina]);
        Assert.Equal(NotificationKind.Assigned, recipients[Omar]);
        Assert.Equal(NotificationKind.Commented, recipients[Bilal]);
    }

    [Fact]
    public void Each_person_gets_one_reason_even_when_listed_twice()
    {
        var recipients = RecipientRules.Resolve([(NotificationKind.StatusChanged, [Bilal, Bilal])], actorId: Amina);

        Assert.Equal(NotificationKind.StatusChanged, Assert.Single(recipients).Value);
    }

    [Theory]
    [InlineData(NotificationKind.Assigned, true)]
    [InlineData(NotificationKind.Mentioned, true)]
    [InlineData(NotificationKind.Commented, false)]
    [InlineData(NotificationKind.StatusChanged, false)]
    [InlineData(NotificationKind.ApprovalRequested, true)]
    [InlineData(NotificationKind.GateDecided, true)]
    [InlineData(NotificationKind.UploadBlocked, true)]
    public void Kinds_aimed_at_the_recipient_follow_the_data_model(NotificationKind kind, bool aimedAtRecipient)
    {
        Assert.Equal(aimedAtRecipient, RecipientRules.IsAimedAtRecipient(kind));
    }

    [Fact]
    public void A_notification_starts_unread_and_can_be_marked_both_ways()
    {
        var notification = Notification.Create(Bilal, NotificationKind.StatusChanged, 1, 2, Amina, "Done", T0);

        Assert.Null(notification.ReadAt);
        notification.MarkRead(T0.AddMinutes(1));
        Assert.Equal(T0.AddMinutes(1), notification.ReadAt);
        notification.MarkRead(T0.AddMinutes(2));
        Assert.Equal(T0.AddMinutes(1), notification.ReadAt);
        notification.MarkUnread();
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void A_long_detail_is_shortened_to_fit()
    {
        var notification = Notification.Create(Bilal, NotificationKind.UploadBlocked, 1, 2, null, new string('x', 300), T0);

        Assert.Equal(Notification.DetailMaxLength, notification.Detail!.Length);
        Assert.EndsWith("…", notification.Detail, StringComparison.Ordinal);
    }
}
