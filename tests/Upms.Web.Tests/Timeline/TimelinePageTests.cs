using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Timeline;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Timeline;

/// <summary>The Timeline view (Phase 2 FR-034–FR-040). "Today" is 27 Sep 2026; in months the track starts on 1 Aug, so
/// 5 Oct is 520 px in and a day is 8 px.</summary>
public sealed class TimelinePageTests : BunitTestBase
{
    private readonly FakeTimelineService _timeline = new();
    private readonly List<string> _announced = [];
    private readonly NavigationManager _navigation;

    public TimelinePageTests()
    {
        Services.AddSingleton<ITimelineService>(_timeline);
        Services.AddSingleton<IWorkItemService>(new FakeWorkItemService());
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        _navigation = Services.GetRequiredService<NavigationManager>();
    }

    private static DateOnly Oct(int day) => new(2026, 10, day);

    private IRenderedComponent<TimelinePage> RenderTimeline(string query = "")
    {
        _navigation.NavigateTo($"projects/WEB/timeline{query}");
        return Render<TimelinePage>(p => p.Add(x => x.Key, "WEB"));
    }

    private static IElement Bar(IRenderedComponent<TimelinePage> cut, string key) => cut.Find($"button.tl-bar[data-key='{key}']");

    private static void Press(IRenderedComponent<TimelinePage> cut, string key, string keyName, bool shift = false, bool ctrl = false) =>
        Bar(cut, key).KeyDown(new KeyboardEventArgs { Key = keyName, ShiftKey = shift, CtrlKey = ctrl });

    private static string Style(IRenderedComponent<TimelinePage> cut, string key) => Bar(cut, key).GetAttribute("style") ?? "";

    [Fact]
    public void P2_US4_AS1_Bars_are_named_with_key_title_dates_status_and_assignee()
    {
        var cut = RenderTimeline();

        Assert.Equal("WEB-1 Plan the launch, 5 Oct 2026 to 10 Oct 2026, In progress, assigned to Bilal Ahmed",
            Bar(cut, "WEB-1").GetAttribute("aria-label"));
        Assert.Equal("WEB-2 Book the venue, due 3 Oct 2026, To do, unassigned", Bar(cut, "WEB-2").GetAttribute("aria-label"));
        Assert.Contains("left:520px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Contains("width:48px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Contains("width:8px", Style(cut, "WEB-2"), StringComparison.Ordinal); // P2_US4_AS2: one day
        Assert.Contains("In progress", Bar(cut, "WEB-1").TextContent, StringComparison.Ordinal);
        Assert.Contains("BA", cut.Find("[data-testid=tl-row][data-key='WEB-1']").TextContent, StringComparison.Ordinal);
        Assert.Equal(["WEB-5", "WEB-2", "WEB-1"], cut.FindAll("[data-testid=tl-row]").Select(r => r.GetAttribute("data-key")));
        Assert.Equal("page", cut.Find("nav.project-nav a[href='projects/WEB/timeline']").GetAttribute("aria-current"));
        Assert.NotNull(cut.Find(".tl-today"));
        Assert.Equal("true", cut.Find("[data-scale=months]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void P2_US4_AS5_Arrow_keys_move_a_bar_by_a_day_and_Enter_saves_once()
    {
        var cut = RenderTimeline();

        Press(cut, "WEB-1", "ArrowRight");
        Press(cut, "WEB-1", "ArrowRight");

        Assert.Contains("left:536px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Equal("WEB-1: 7 Oct 2026 to 12 Oct 2026. Enter saves, Escape cancels.", _announced[^1]);
        Press(cut, "WEB-1", "ArrowRight", shift: true);
        Assert.Equal("WEB-1: 7 Oct 2026 to 13 Oct 2026. Enter saves, Escape cancels.", _announced[^1]);
        Press(cut, "WEB-1", "ArrowLeft", ctrl: true);
        Assert.Equal("WEB-1: 6 Oct 2026 to 13 Oct 2026. Enter saves, Escape cancels.", _announced[^1]);
        Assert.Empty(_timeline.Reschedules);

        Press(cut, "WEB-1", "Enter");

        Assert.Equal(("WEB-1", (DateOnly?)Oct(6), (DateOnly?)Oct(13)), Single(_timeline.Reschedules));
        Assert.Equal("WEB-1 now runs from 6 Oct 2026 to 13 Oct 2026.", _announced[^1]);
        Assert.Contains("left:528px", Style(cut, "WEB-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void P2_US4_AS5_Escape_cancels_without_saving()
    {
        var cut = RenderTimeline();

        Press(cut, "WEB-1", "ArrowRight");
        Press(cut, "WEB-1", "Escape");

        Assert.Empty(_timeline.Reschedules);
        Assert.Contains("left:520px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Equal("Change to WEB-1 cancelled.", _announced[^1]);
    }

    [Fact]
    public void P2_US4_AS4_The_due_date_never_moves_before_the_start_date()
    {
        var cut = RenderTimeline();

        for (var i = 0; i < 6; i++)
        {
            Press(cut, "WEB-1", "ArrowLeft", shift: true);
        }

        Assert.Equal("The due date cannot be before the start date.", _announced[^1]);
        Press(cut, "WEB-1", "Enter");
        Assert.Equal(("WEB-1", (DateOnly?)Oct(5), (DateOnly?)Oct(5)), Single(_timeline.Reschedules));
    }

    [Fact]
    public void A_task_with_only_a_due_date_gains_a_start_date_with_Ctrl()
    {
        var cut = RenderTimeline();

        Press(cut, "WEB-2", "ArrowLeft", ctrl: true);
        Press(cut, "WEB-2", "Enter");

        Assert.Equal(("WEB-2", (DateOnly?)Oct(2), (DateOnly?)Oct(3)), Single(_timeline.Reschedules));
    }

    [Fact]
    public void Enter_with_nothing_pending_opens_the_drawer()
    {
        var cut = RenderTimeline("?scale=weeks");

        Press(cut, "WEB-1", "Enter");

        Assert.EndsWith("?scale=weeks&task=WEB-1", _navigation.Uri, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid=drawer]"));
        Assert.Empty(_timeline.Reschedules);
    }

    [Fact]
    public void P2_US4_AS6_Schedule_gives_an_unscheduled_task_today_and_six_days_later()
    {
        var cut = RenderTimeline();
        Assert.Equal(["WEB-4"], cut.FindAll("[data-testid=unscheduled-item]").Select(i => i.GetAttribute("data-key")));

        cut.Find("[data-testid=unscheduled-item][data-key='WEB-4'] button.schedule-btn").Click();

        Assert.Equal(("WEB-4", (DateOnly?)new DateOnly(2026, 9, 27), (DateOnly?)Oct(3)), Single(_timeline.Reschedules));
        Assert.NotNull(Bar(cut, "WEB-4"));
        Assert.Empty(cut.FindAll("[data-testid=unscheduled-item]"));
        Assert.Equal("WEB-4 scheduled from 27 Sep 2026 to 3 Oct 2026.", _announced[^1]);
        Assert.Equal("tl-bar-WEB-4", JSInterop.Invocations.Last(i => i.Identifier == "upms.focusByIdIfLost").Arguments[0]);
    }

    [Fact]
    public void P2_US4_AS7_Expanding_a_task_shows_its_sub_tasks()
    {
        var cut = RenderTimeline();
        Assert.Empty(cut.FindAll("[data-testid=tl-subrow]"));

        cut.Find("[data-testid=tl-row][data-key='WEB-5'] button.tl-expand").Click();

        Assert.Equal("true", cut.Find("[data-testid=tl-row][data-key='WEB-5'] button.tl-expand").GetAttribute("aria-expanded"));
        Assert.Equal(["WEB-7"], cut.FindAll("[data-testid=tl-subrow]").Select(r => r.GetAttribute("data-key")));
        Assert.Equal("WEB-7 Record the video, starts 1 Oct 2026, To do, unassigned", Bar(cut, "WEB-7").GetAttribute("aria-label"));
        Assert.Contains("WEB-8", cut.Find("[data-testid=tl-unscheduled-subtasks]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P2_US4_AS8_A_conflict_explains_and_shows_the_current_dates()
    {
        var current = FakeTimelineService.Item("WEB-1", "Plan the launch", FakeTimelineService.InProgress, Oct(12), Oct(15));
        _timeline.NextResult = () => AppError.Conflict("WEB-1 was changed by someone else. The timeline now shows its current dates.", current);
        var cut = RenderTimeline();

        Press(cut, "WEB-1", "ArrowRight");
        Press(cut, "WEB-1", "Enter");

        Assert.Contains("WEB-1 was changed by someone else.", cut.Find("[data-testid=conflict]").TextContent, StringComparison.Ordinal);
        Assert.Contains("left:576px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Contains("width:32px", Style(cut, "WEB-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void P2_US4_AS9_Viewers_read_the_timeline_but_cannot_change_it()
    {
        _timeline.CanContribute = false;
        var cut = RenderTimeline();

        Press(cut, "WEB-1", "ArrowRight");
        cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-1", "move", 3));

        Assert.Contains("left:520px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".tl-handle"));
        Assert.Empty(cut.FindAll("button.schedule-btn"));
        Assert.Empty(_timeline.Reschedules);
        Press(cut, "WEB-1", "Enter");
        Assert.Contains("task=WEB-1", _navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Keys_pressed_while_a_change_is_being_saved_build_on_it_and_the_next_save_waits_for_it()
    {
        // Over a slow connection the next key can arrive before the save has answered.
        var cut = RenderTimeline();
        _timeline.Gate = new TaskCompletionSource();

        Press(cut, "WEB-1", "ArrowLeft");
        Press(cut, "WEB-1", "Enter");                   // 4–9 Oct, being saved
        Press(cut, "WEB-1", "ArrowRight", shift: true); // builds on 4–9 Oct
        Press(cut, "WEB-1", "Enter");                   // waits for the first save

        Assert.Equal(("WEB-1", (DateOnly?)Oct(4), (DateOnly?)Oct(9)), Single(_timeline.Reschedules));
        cut.InvokeAsync(_timeline.Gate.SetResult);

        cut.WaitForAssertion(() => Assert.Equal(2, _timeline.Reschedules.Count));
        Assert.Equal(("WEB-1", (DateOnly?)Oct(4), (DateOnly?)Oct(10), 9), (_timeline.Reschedules[1].Key, _timeline.Reschedules[1].Start,
            _timeline.Reschedules[1].Due, (int)_timeline.Reschedules[1].Version.Single())); // with the version the first save returned
        cut.WaitForAssertion(() => Assert.Equal("WEB-1 Plan the launch, 4 Oct 2026 to 10 Oct 2026, In progress, assigned to Bilal Ahmed",
            Bar(cut, "WEB-1").GetAttribute("aria-label")));
        Assert.Empty(cut.FindAll("[data-testid=conflict]"));
    }

    [Fact]
    public void A_change_queued_behind_a_save_that_fails_is_dropped()
    {
        var current = FakeTimelineService.Item("WEB-1", "Plan the launch", FakeTimelineService.InProgress, Oct(12), Oct(15));
        _timeline.NextResult = () => AppError.Conflict("WEB-1 was changed by someone else. The timeline now shows its current dates.", current);
        var cut = RenderTimeline();
        _timeline.Gate = new TaskCompletionSource();

        Press(cut, "WEB-1", "ArrowLeft");
        Press(cut, "WEB-1", "Enter");
        Press(cut, "WEB-1", "ArrowRight", shift: true);
        Press(cut, "WEB-1", "Enter"); // built on a change that will be refused
        cut.InvokeAsync(_timeline.Gate.SetResult);

        cut.WaitForAssertion(() => Assert.Contains("WEB-1 was changed by someone else.", cut.Find("[data-testid=conflict]").TextContent,
            StringComparison.Ordinal));
        Assert.Equal(("WEB-1", (DateOnly?)Oct(4), (DateOnly?)Oct(9)), Single(_timeline.Reschedules));
        Assert.Equal("WEB-1 Plan the launch, 12 Oct 2026 to 15 Oct 2026, In progress, unassigned", Bar(cut, "WEB-1").GetAttribute("aria-label"));
    }

    [Fact]
    public void Hide_completed_is_kept_in_the_address()
    {
        var cut = RenderTimeline();

        cut.Find("[data-testid=hide-completed]").Click();

        Assert.EndsWith("?completed=hide", _navigation.Uri, StringComparison.Ordinal);
        Assert.True(_timeline.Loads[^1]);
        Assert.Equal("true", cut.Find("[data-testid=hide-completed]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Switching_the_scale_keeps_a_pending_change()
    {
        var cut = RenderTimeline();
        Press(cut, "WEB-1", "ArrowRight");

        cut.Find("[data-scale=weeks]").Click();

        Assert.EndsWith("?scale=weeks", _navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("width:216px", Style(cut, "WEB-1"), StringComparison.Ordinal);
        Press(cut, "WEB-1", "Enter");
        Assert.Equal(("WEB-1", (DateOnly?)Oct(6), (DateOnly?)Oct(11)), Single(_timeline.Reschedules));
    }

    [Theory]
    [InlineData("move", 14, 19, 24)]
    [InlineData("end", 3, 5, 13)]
    [InlineData("end", -10, 5, 5)]
    [InlineData("start", 7, 10, 10)]
    [InlineData("start", -2, 3, 10)]
    public void Dragging_moves_the_bar_or_one_end_by_whole_days(string mode, int days, int start, int due)
    {
        var cut = RenderTimeline();

        cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-1", mode, days));

        Assert.Equal(("WEB-1", (DateOnly?)Oct(start), (DateOnly?)Oct(due)), Single(_timeline.Reschedules));
    }

    [Theory]
    [InlineData("move", int.MaxValue)]
    [InlineData("start", int.MinValue)]
    [InlineData("end", 40_000)]
    public async Task An_offset_no_drag_can_produce_is_ignored(string mode, int days)
    {
        // The offset comes from the browser, so a page changed by its user could send anything (security review F1).
        var cut = RenderTimeline();

        await cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-1", mode, days));

        Assert.Empty(_timeline.Reschedules);
        Assert.Contains("left:520px", Style(cut, "WEB-1"), StringComparison.Ordinal);
    }

    [Fact]
    public void Today_scrolls_the_track_to_today()
    {
        var cut = RenderTimeline();

        cut.Find("[data-testid=go-today]").Click();

        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.timeline.scrollToToday");
    }

    [Fact]
    public void With_nothing_scheduled_the_timeline_points_to_Schedule()
    {
        _timeline.Rows = [];

        var cut = RenderTimeline();

        Assert.Contains("No task has dates yet.", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("button.schedule-btn"));
    }

    private static (string Key, DateOnly? Start, DateOnly? Due) Single(List<(string Key, DateOnly? Start, DateOnly? Due, byte[] Version)> calls)
    {
        var call = Assert.Single(calls);
        return (call.Key, call.Start, call.Due);
    }
}
