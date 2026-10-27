using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Timeline;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Timeline;

/// <summary>The timeline as a plan: stages with progress and gates, milestones, the sprint lane, dependency arrows and
/// each row's Actions menu (Phase 3 FR-042–FR-047, FR-050). "Today" is 27 Sep 2026.</summary>
public sealed class TimelinePlanPageTests : BunitTestBase
{
    private static readonly StatusOption ToDo = FakeTimelineService.ToDo;
    private readonly FakeTimelineService _timeline = new() { Template = ProjectTemplate.StageGate };
    private readonly FakeDependencyService _dependencies = new();
    private readonly List<string> _announced = [];

    public TimelinePlanPageTests()
    {
        Services.AddSingleton<ITimelineService>(_timeline);
        Services.AddSingleton<IDependencyService>(_dependencies);
        Services.AddSingleton<IWorkItemService>(new FakeWorkItemService());
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        _timeline.Groups =
        [
            new TimelineGroup(Item("WEB-10", "Initiation", Oct(1), Oct(10), WorkItemType.Stage), new Progress(1, 2), new GateMarker(Oct(10), GateState.Approved),
                [
                    new TimelineRow(Item("WEB-11", "Write the charter", Oct(2), Oct(5)), [], []),
                    new TimelineRow(Item("WEB-13", "Charter signed", null, Oct(8), WorkItemType.Milestone), [], []),
                ],
                [Item("WEB-12", "Agree the budget", null, null)]),
            new TimelineGroup(Item("WEB-20", "Planning", null, null, WorkItemType.Stage), new Progress(0, 0), null, [],
                [Item("WEB-21", "Planning sign-off", null, null, WorkItemType.Milestone)]),
        ];
    }

    private static DateOnly Oct(int day) => new(2026, 10, day);

    private static TimelineItem Item(string key, string title, DateOnly? start, DateOnly? due, WorkItemType type = WorkItemType.Task) =>
        FakeTimelineService.Item(key, title, ToDo, start, due) with { Type = type };

    private IRenderedComponent<TimelinePage> RenderTimeline()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("spaces/WEB/timeline");
        return Render<TimelinePage>(p => p.Add(x => x.Key, "WEB"));
    }

    private static IElement Group(IRenderedComponent<TimelinePage> cut, string key) => cut.Find($"[data-testid=tl-group][data-key='{key}']");

    private static void Act(IRenderedComponent<TimelinePage> cut, string key, string action)
    {
        cut.Find($"#tl-actions-{key}").Click();
        cut.FindAll($"#tl-actions-{key}-menu button").Single(b => b.TextContent.Trim() == action).Click();
    }

    [Fact]
    public void P3_US4_AS1_Stages_are_bars_with_progress_that_expand_and_collapse()
    {
        var cut = RenderTimeline();

        Assert.Equal(["WEB-10", "WEB-20"], cut.FindAll("[data-testid=tl-group]").Select(g => g.GetAttribute("data-key")));
        var initiation = Group(cut, "WEB-10");
        Assert.Equal("50% done", initiation.QuerySelector("[data-testid=tl-progress]")!.TextContent.Trim());
        Assert.NotNull(initiation.QuerySelector("#tl-bar-WEB-10"));
        // A stage without dates has no bar but "Schedule", as Jira's timeline shows it.
        Assert.Null(Group(cut, "WEB-20").QuerySelector("#tl-bar-WEB-20"));
        Assert.Equal("Schedule WEB-20", Group(cut, "WEB-20").QuerySelector("button.tl-schedule")!.GetAttribute("aria-label"));
        Assert.NotEmpty(cut.FindAll("[data-testid=tl-row][data-key='WEB-11']"));
        Assert.NotNull(initiation.QuerySelector("[data-testid=tl-group-unscheduled][data-key='WEB-12']"));

        initiation.QuerySelector("button.tl-expand")!.Click();

        Assert.Empty(cut.FindAll("[data-testid=tl-row][data-key='WEB-11']"));
        Assert.Empty(cut.FindAll("[data-testid=tl-group-unscheduled][data-key='WEB-12']"));
        Assert.Equal("false", Group(cut, "WEB-10").QuerySelector("button.tl-expand")!.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void P3_US4_AS2_A_milestone_is_a_marker_that_moves_a_day_per_arrow_key()
    {
        var cut = RenderTimeline();
        var marker = cut.Find("button.tl-milestone[data-key='WEB-13']");
        Assert.Contains("milestone on 8 Oct 2026", marker.GetAttribute("aria-label"), StringComparison.Ordinal);
        Assert.Empty(marker.QuerySelectorAll(".tl-handle"));

        marker.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.Find("button.tl-milestone[data-key='WEB-13']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", ShiftKey = true });
        cut.Find("button.tl-milestone[data-key='WEB-13']").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(("WEB-13", (DateOnly?)null, (DateOnly?)Oct(10)), (_timeline.Reschedules[0].Key, _timeline.Reschedules[0].Start, _timeline.Reschedules[0].Due));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("move")]
    public async Task A_milestone_dragged_by_either_end_only_moves_its_date(string mode)
    {
        // A milestone has no handles, but the mode comes from the browser (Phase 3 security review F1).
        var cut = RenderTimeline();

        await cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-13", mode, 2));

        Assert.Equal(("WEB-13", (DateOnly?)null, (DateOnly?)Oct(10)), (_timeline.Reschedules[0].Key, _timeline.Reschedules[0].Start, _timeline.Reschedules[0].Due));
    }

    [Fact]
    public void P3_US4_AS3_A_stage_s_gate_is_a_marker_labelled_with_its_state()
    {
        var cut = RenderTimeline();

        var gate = Group(cut, "WEB-10").QuerySelector("[data-testid=gate-marker]")!;
        Assert.Contains("Gate approved", gate.TextContent, StringComparison.Ordinal);
        Assert.Null(Group(cut, "WEB-20").QuerySelector("[data-testid=gate-marker]"));
    }

    [Fact]
    public void P3_US4_AS8_A_Scrum_project_shows_its_sprints_in_a_lane()
    {
        _timeline.Template = ProjectTemplate.Scrum;
        _timeline.Sprints = [new SprintBar("WEB Sprint 1", Oct(5), Oct(19), SprintState.Completed)];

        var cut = RenderTimeline();

        var bar = cut.Find("[data-testid=sprint-lane] [data-testid=sprint-bar]");
        Assert.Contains("WEB Sprint 1", bar.TextContent, StringComparison.Ordinal);
        Assert.Contains("completed", bar.TextContent, StringComparison.Ordinal);
        Assert.Contains("5 Oct 2026 to 19 Oct 2026", bar.TextContent, StringComparison.Ordinal); // for screen readers
    }

    [Fact]
    public void P3_US4_AS6_Arrows_are_hidden_from_assistive_technology_and_the_bar_says_what_it_waits_for()
    {
        _timeline.Rows = [new TimelineRow(FakeTimelineService.Item("WEB-1", "Plan the launch", ToDo, Oct(5), Oct(10)) with
        {
            WaitsFor = [new WaitingOn("WEB-11", IsLate: true)],
        }, [], [])];
        _timeline.Dependencies = [new TimelineDependency("WEB-11", "WEB-1", IsLate: true)];

        var cut = RenderTimeline();

        Assert.Equal("true", cut.Find("svg.tl-arrows").GetAttribute("aria-hidden"));
        Assert.Contains("waits for WEB-11 (late)", cut.Find("#tl-bar-WEB-1").GetAttribute("aria-label"), StringComparison.Ordinal);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.timeline.drawArrows");
    }

    [Fact]
    public async Task P3_US4_AS6_Moving_a_linked_bar_reads_the_timeline_again_so_its_late_marks_follow()
    {
        _timeline.Dependencies = [new TimelineDependency("WEB-11", "WEB-13", IsLate: false)];
        var cut = RenderTimeline();
        var loads = _timeline.Loads.Count;

        await cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-11", "end", 4));
        Assert.Equal(loads + 1, _timeline.Loads.Count);

        await cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-12", "move", 1)); // not shown with dates: nothing to save
        await cut.InvokeAsync(() => cut.Instance.OnBarDragged("WEB-1", "move", 1)); // not linked: no need to read again
        Assert.Equal(loads + 1, _timeline.Loads.Count);
    }

    [Fact]
    public void P3_US4_AS4_Schedule_gives_a_stage_four_weeks_and_a_milestone_today()
    {
        var cut = RenderTimeline();

        Act(cut, "WEB-20", "Schedule");
        Assert.Equal(("WEB-20", (DateOnly?)new DateOnly(2026, 9, 27), (DateOnly?)new DateOnly(2026, 10, 24)),
            (_timeline.Reschedules[^1].Key, _timeline.Reschedules[^1].Start, _timeline.Reschedules[^1].Due));

        Act(cut, "WEB-21", "Schedule");
        Assert.Equal(("WEB-21", (DateOnly?)null, (DateOnly?)new DateOnly(2026, 9, 27)),
            (_timeline.Reschedules[^1].Key, _timeline.Reschedules[^1].Start, _timeline.Reschedules[^1].Due));
    }

    [Fact]
    public void P3_US4_AS5_A_dependency_is_added_from_a_row_s_Actions_menu()
    {
        var cut = RenderTimeline();

        Act(cut, "WEB-11", "Add dependency");
        cut.Find("#dependency-search").Input("app");
        cut.FindAll("[data-testid=dependency-result]")[0].Click();

        Assert.Equal([("WEB-11", "WEB-6")], _dependencies.Added);
        Assert.Contains("WEB-11 now waits for WEB-6.", _announced);
    }

    [Fact]
    public void Escape_closes_a_row_s_actions_and_puts_focus_back_on_its_button()
    {
        var cut = RenderTimeline();
        cut.Find("#tl-actions-WEB-11").Click();
        Assert.Equal("true", cut.Find("#tl-actions-WEB-11").GetAttribute("aria-expanded"));
        Assert.Equal("group", cut.Find("#tl-actions-WEB-11-menu").GetAttribute("role"));

        cut.Find("#tl-actions-WEB-11-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll("#tl-actions-WEB-11-menu"));
        Assert.Equal("false", cut.Find("#tl-actions-WEB-11").GetAttribute("aria-expanded"));
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusByIdIfLost" && Equals(i.Arguments[0], "tl-actions-WEB-11"));
    }

    [Fact]
    public void P3_US4_AS9_Viewers_move_nothing_and_add_no_dependencies()
    {
        _timeline.CanContribute = false;

        var cut = RenderTimeline();

        cut.Find("button.tl-milestone[data-key='WEB-13']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.Find("#tl-actions-WEB-11").Click();

        Assert.Equal(["Open"], cut.FindAll("#tl-actions-WEB-11-menu button").Select(b => b.TextContent.Trim()));
        Assert.Empty(_timeline.Reschedules);
    }
}
