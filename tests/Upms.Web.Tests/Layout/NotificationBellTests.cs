using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Notifications;
using Upms.Domain.Common;
using Upms.Web.Components.Layout;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Layout;

/// <summary>The bell on every screen and its panel (Phase 4 FR-018, FR-019; research R9, R25).</summary>
public sealed class NotificationBellTests : BunitTestBase
{
    private readonly FakeNotificationService _notifications = new();

    public NotificationBellTests()
    {
        Services.AddSingleton<INotificationService>(_notifications);
    }

    private static NotificationView Notification(long id, NotificationKind kind, string? detail = null, bool read = false, int minutesAgo = 0,
        string key = "WEB-1", string title = "Plan the launch") =>
        new(id, kind, kind == NotificationKind.UploadBlocked ? null : "Amina Khan", key[..key.IndexOf('-')], key, title, detail,
            Start.AddMinutes(-minutesAgo), read);

    [Fact]
    public void P4_FR018_The_bell_is_named_with_the_unread_count()
    {
        _notifications.Items.AddRange([Notification(1, NotificationKind.Mentioned), Notification(2, NotificationKind.Commented),
            Notification(3, NotificationKind.Assigned, read: true)]);

        var cut = Render<NotificationBell>();

        Assert.Equal("Notifications, 2 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label"));
        Assert.Equal("2", cut.Find(".bell-count").TextContent.Trim());
    }

    [Fact]
    public void P4_SC001_The_count_refreshes_every_30_seconds()
    {
        var cut = Render<NotificationBell>();
        Assert.Equal("Notifications, 0 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label"));

        _notifications.Items.Add(Notification(1, NotificationKind.Mentioned));
        Time.Advance(TimeSpan.FromSeconds(30));

        cut.WaitForAssertion(() => Assert.Equal("Notifications, 1 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label")));
    }

    [Fact]
    public void P4_FR019_The_panel_lists_newest_first_with_who_what_which_item_and_when()
    {
        _notifications.Items.AddRange([
            Notification(1, NotificationKind.Mentioned, minutesAgo: 10),
            Notification(2, NotificationKind.StatusChanged, "Done", minutesAgo: 5, key: "WEB-4", title: "Book the venue"),
        ]);
        var cut = Render<NotificationBell>();

        cut.Find("[data-testid=bell]").Click();

        var panel = cut.Find("#notification-panel");
        Assert.Equal(("dialog", "false"), (panel.GetAttribute("role"), panel.GetAttribute("aria-modal")));
        Assert.Equal("true", cut.Find("[data-testid=bell]").GetAttribute("aria-expanded"));
        var items = cut.FindAll(".notification");
        Assert.Equal(["Amina Khan moved WEB-4 Book the venue to Done", "Amina Khan mentioned you on WEB-1 Plan the launch"],
            items.Select(i => i.QuerySelector("a")!.TextContent.Trim()));
        Assert.All(items, i => Assert.NotNull(i.QuerySelector("time")));
        Assert.All(items, i => Assert.Contains("unread", i.ClassName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(NotificationKind.Assigned, null, "Amina Khan assigned WEB-1 Plan the launch to you")]
    [InlineData(NotificationKind.Commented, null, "Amina Khan commented on WEB-1 Plan the launch")]
    [InlineData(NotificationKind.ApprovalRequested, null, "Amina Khan requested approval of the Plan the launch gate in WEB")]
    [InlineData(NotificationKind.GateDecided, "Approved", "The Plan the launch gate in WEB was approved")]
    [InlineData(NotificationKind.GateDecided, "Sent back", "The Plan the launch gate in WEB was sent back")]
    [InlineData(NotificationKind.UploadBlocked, "report.pdf", "report.pdf on WEB-1 was blocked by the malware check")]
    public void Each_kind_reads_as_a_sentence(NotificationKind kind, string? detail, string expected)
    {
        Assert.Equal(expected, NotificationText.Sentence(Notification(1, kind, detail)));
    }

    [Fact]
    public void P4_FR019_Opening_a_notification_marks_it_read_and_opens_the_items_drawer()
    {
        _notifications.Items.Add(Notification(7, NotificationKind.Mentioned));
        var cut = Render<NotificationBell>();
        cut.Find("[data-testid=bell]").Click();

        cut.Find(".notification a").Click();

        Assert.Equal([(7L, true)], _notifications.Marks);
        Assert.EndsWith("spaces/WEB/board?task=WEB-1", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#notification-panel")));
        Assert.Equal("Notifications, 0 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label"));
    }

    [Fact]
    public void Each_notification_can_be_marked_read_or_unread_and_all_as_read()
    {
        _notifications.Items.AddRange([Notification(1, NotificationKind.Mentioned), Notification(2, NotificationKind.Commented, minutesAgo: 1)]);
        var cut = Render<NotificationBell>();
        cut.Find("[data-testid=bell]").Click();

        cut.Find("[data-testid=mark-1]").Click();
        cut.WaitForAssertion(() => Assert.Equal("Notifications, 1 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label")));
        Assert.Equal("Mark as unread: Amina Khan mentioned you on WEB-1 Plan the launch", cut.Find("[data-testid=mark-1]").GetAttribute("aria-label"));

        cut.Find("[data-testid=mark-all-read]").Click();
        cut.WaitForAssertion(() => Assert.Equal("Notifications, 0 unread", cut.Find("[data-testid=bell]").GetAttribute("aria-label")));
    }

    [Fact]
    public void Show_more_loads_the_next_20()
    {
        for (var i = 1; i <= 25; i++)
        {
            _notifications.Items.Add(Notification(i, NotificationKind.Commented, minutesAgo: i));
        }

        var cut = Render<NotificationBell>();
        cut.Find("[data-testid=bell]").Click();
        Assert.Equal(20, cut.FindAll(".notification").Count);

        cut.Find("[data-testid=more-notifications]").Click();

        cut.WaitForAssertion(() => Assert.Equal(25, cut.FindAll(".notification").Count));
        Assert.Empty(cut.FindAll("[data-testid=more-notifications]"));
    }

    [Fact]
    public void An_empty_panel_says_so()
    {
        var cut = Render<NotificationBell>();

        cut.Find("[data-testid=bell]").Click();

        Assert.Contains("No notifications yet.", cut.Find("#notification-panel").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_closes_the_panel_and_puts_focus_back_on_the_bell()
    {
        var cut = Render<NotificationBell>();
        cut.Find("[data-testid=bell]").Click();

        cut.Find("#notification-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("#notification-panel"));
        Assert.Equal("false", cut.Find("[data-testid=bell]").GetAttribute("aria-expanded"));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "Blazor._internal.domWrapper.focus");
    }
}
