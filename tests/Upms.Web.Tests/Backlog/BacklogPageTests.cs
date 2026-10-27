using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Backlog;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Backlog;

/// <summary>The Backlog view of a Scrum project (Phase 3 FR-021–FR-026, FR-028; contracts/ui-routes.md). "Today" is
/// 27 Sep 2026.</summary>
public sealed class BacklogPageTests : BunitTestBase
{
    private readonly FakeBacklogService _backlog = new();
    private readonly FakeSprintService _sprints = new();
    private readonly FakeWorkItemService _items = new();
    private readonly List<string> _announced = [];

    public BacklogPageTests()
    {
        Services.AddSingleton<IBacklogService>(_backlog);
        Services.AddSingleton<ISprintService>(_sprints);
        Services.AddSingleton<IWorkItemService>(_items);
        Services.AddSingleton<ICommentService>(new FakeCommentService(CurrentUser.UserId!.Value));
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
    }

    private IRenderedComponent<BacklogPage> RenderBacklog(string key = "MOB") => Render<BacklogPage>(p => p.Add(x => x.Key, key));

    private static List<string> KeysIn(IElement section) =>
        section.QuerySelectorAll("[data-testid=backlog-row]").Select(r => r.GetAttribute("data-key")!).ToList();

    private static IElement Sprint(IRenderedComponent<BacklogPage> cut, long id) => cut.Find($"[data-testid=sprint-section][data-sprint='{id}']");

    private static IElement BacklogSection(IRenderedComponent<BacklogPage> cut) => cut.Find("[data-testid=backlog-section]");

    private static IElement Row(IRenderedComponent<BacklogPage> cut, string key) => cut.Find($"[data-testid=backlog-row][data-key='{key}']");

    private static List<string> MenuItems(IRenderedComponent<BacklogPage> cut, string key)
    {
        cut.Find($"#move-{key}").Click();
        return cut.FindAll($"[data-key='{key}'] .move-list button").Select(b => b.TextContent.Trim()).ToList();
    }

    private static void Choose(IRenderedComponent<BacklogPage> cut, string key, string item)
    {
        cut.Find($"#move-{key}").Click();
        cut.FindAll($"[data-key='{key}'] .move-list button").Single(b => b.TextContent.Trim() == item).Click();
    }

    [Fact]
    public void P3_US2_AS1_The_sprints_come_first_then_the_backlog_with_rows_and_totals()
    {
        var cut = RenderBacklog();

        Assert.Equal(["MOB Sprint 1", "MOB Sprint 2"], cut.FindAll("[data-testid=sprint-section] h2").Select(h => h.TextContent.Trim()));
        var active = Sprint(cut, FakeBacklogService.ActiveId);
        Assert.Equal(["MOB-2", "MOB-3"], KeysIn(active));
        Assert.Contains("2 items · 5 points", active.QuerySelector("[data-testid=sprint-totals]")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("Done: 2 · Remaining: 3", active.QuerySelector("[data-testid=sprint-progress]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("Ship card refunds", active.QuerySelector("[data-testid=sprint-goal]")!.TextContent.Trim());
        Assert.Equal("21 Sep – 5 Oct 2026", active.QuerySelector("[data-testid=sprint-dates]")!.TextContent.Trim());
        Assert.Contains("1 item · 0 points · 1 without estimate",
            Sprint(cut, FakeBacklogService.PlannedId).QuerySelector("[data-testid=sprint-totals]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal(["MOB-5", "MOB-6", "MOB-7"], KeysIn(BacklogSection(cut)));

        var row = Row(cut, "MOB-2");
        Assert.Equal("Story", row.QuerySelector(".type-icon .sr-only")!.TextContent.Trim());
        Assert.Equal("Refund to card", row.QuerySelector(".brow-title")!.TextContent.Trim());
        Assert.Equal("spaces/MOB/backlog?task=MOB-2", row.QuerySelector(".brow-title")!.GetAttribute("href"));
        Assert.Equal("MOB-1 Card refunds", row.QuerySelector("[data-testid=row-epic]")!.TextContent.Trim());
        Assert.Equal("3 points", row.QuerySelector("[data-testid=row-estimate]")!.TextContent.Trim());
        Assert.Equal("To Do", row.QuerySelector("[data-testid=row-status]")!.TextContent.Trim());
        Assert.Equal("page", cut.Find("nav.project-nav a[href='spaces/MOB/backlog']").GetAttribute("aria-current"));
    }

    [Fact]
    public void An_active_sprint_past_its_end_is_marked_overdue()
    {
        _backlog.View = _backlog.View with { Active = _backlog.View.Active! with { EndDate = new DateOnly(2026, 9, 25) } };

        var cut = RenderBacklog();

        Assert.Equal("Overdue", Sprint(cut, FakeBacklogService.ActiveId).QuerySelector("[data-testid=sprint-overdue]")!.TextContent.Trim());
    }

    [Fact]
    public void P3_US2_AS3_The_move_menu_names_each_place_the_row_can_go()
    {
        var cut = RenderBacklog();

        Assert.Equal(["Move to top", "Move to bottom", "Move up", "Move down", "Move to MOB Sprint 1", "Move to MOB Sprint 2"],
            MenuItems(cut, "MOB-6"));
        Assert.Equal(["Move to top", "Move to bottom", "Move up", "Move down", "Move to MOB Sprint 1", "Move to backlog"],
            MenuItems(cut, "MOB-4"));
    }

    [Fact]
    public void P3_US2_AS2_Move_to_top_reorders_announces_and_keeps_focus_on_the_row()
    {
        var cut = RenderBacklog();

        Choose(cut, "MOB-7", "Move to top");

        var move = Assert.Single(_backlog.Moves);
        Assert.Equal(("MOB-7", BacklogTarget.ToBacklog, BacklogPlacement.AtTop), (move.Key, move.Target, move.Placement));
        cut.WaitForAssertion(() => Assert.Equal(["MOB-7", "MOB-5", "MOB-6"], KeysIn(BacklogSection(cut))));
        Assert.Contains("MOB-7 moved to position 1 of 3 in the backlog.", _announced);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "upms.focusById" && Equals(i.Arguments[0], "move-MOB-7"));
    }

    [Fact]
    public void P3_US2_AS3_Moving_to_a_sprint_keeps_the_rank_and_says_where_the_row_went()
    {
        var cut = RenderBacklog();

        Choose(cut, "MOB-5", "Move to MOB Sprint 2");

        var move = Assert.Single(_backlog.Moves);
        Assert.Equal(new BacklogTarget.Sprint(FakeBacklogService.PlannedId), move.Target);
        Assert.Equal(BacklogPlacement.KeepRank, move.Placement);
        cut.WaitForAssertion(() => Assert.Equal(["MOB-4", "MOB-5"], KeysIn(Sprint(cut, FakeBacklogService.PlannedId))));
        Assert.Contains("MOB-5 moved to MOB Sprint 2, position 2 of 2.", _announced);
    }

    [Fact]
    public void P3_US2_AS2_Dropping_a_row_on_another_moves_it_before_that_row_in_that_list()
    {
        var cut = RenderBacklog();

        Row(cut, "MOB-6").DragStart();
        Row(cut, "MOB-2").Drop();

        var move = Assert.Single(_backlog.Moves);
        Assert.Equal((new BacklogTarget.Sprint(FakeBacklogService.ActiveId), (BacklogPlacement)new BacklogPlacement.Before("MOB-2")),
            (move.Target, move.Placement));
    }

    [Fact]
    public void Dropping_a_row_at_the_end_of_a_list_puts_it_at_the_bottom()
    {
        var cut = RenderBacklog();

        Row(cut, "MOB-5").DragStart();
        Sprint(cut, FakeBacklogService.PlannedId).QuerySelector("[data-testid=list-drop-end]")!.Drop();

        var move = Assert.Single(_backlog.Moves);
        Assert.Equal((new BacklogTarget.Sprint(FakeBacklogService.PlannedId), BacklogPlacement.AtBottom), (move.Target, move.Placement));
    }

    [Fact]
    public void A_refused_move_is_explained_and_the_backlog_is_read_again()
    {
        _backlog.NextMoveResult = () => AppError.Rule(ErrorCodes.SprintCompleted, "MOB Sprint 1 is completed and can no longer change.");
        var cut = RenderBacklog();
        var loads = _backlog.PagesRead.Count;

        Choose(cut, "MOB-5", "Move to top");

        cut.WaitForAssertion(() => Assert.Contains("can no longer change", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal));
        Assert.True(_backlog.PagesRead.Count > loads);
    }

    [Fact]
    public void Show_more_adds_the_next_hundred_rows()
    {
        _backlog.View = _backlog.View with { Backlog = _backlog.View.Backlog with { TotalCount = 5 } };
        _backlog.SecondPage = [FakeBacklogService.Row("MOB-8", WorkItemType.Task, "Refund audit", 1), FakeBacklogService.Row("MOB-9", WorkItemType.Task, "Refund export", 2)];
        var cut = RenderBacklog();

        var more = cut.Find("[data-testid=show-more]");
        Assert.Contains("2 more", more.TextContent, StringComparison.Ordinal);
        more.Click();

        cut.WaitForAssertion(() => Assert.Equal(["MOB-5", "MOB-6", "MOB-7", "MOB-8", "MOB-9"], KeysIn(BacklogSection(cut))));
        Assert.Contains(2, _backlog.PagesRead);
        Assert.Empty(cut.FindAll("[data-testid=show-more]"));
    }

    [Fact]
    public void P3_FR021_What_needs_to_be_done_adds_a_story_at_the_bottom_of_the_backlog()
    {
        var cut = RenderBacklog();
        var create = BacklogSection(cut).QuerySelector(".inline-create")!;

        Assert.Equal(["Story", "Task", "Bug"], create.QuerySelectorAll("select.type-choice option").Select(o => o.TextContent.Trim()));
        Assert.Equal("Story", create.QuerySelector("select.type-choice")!.GetAttribute("value"));
        cut.Find("[data-testid=backlog-section] input.inline-input").Input("Refund receipts");
        cut.Find("[data-testid=backlog-section] input.inline-input").KeyDown("Enter");

        var (projectKey, item) = Assert.Single(_items.Created);
        Assert.Equal(("MOB", new NewWorkItem(WorkItemType.Story, "Refund receipts")), (projectKey, item));
    }

    [Fact]
    public void P3_US2_AS3_Project_Admins_create_sprints()
    {
        var cut = RenderBacklog();

        cut.Find("[data-testid=create-sprint]").Click();

        Assert.Equal(1, _sprints.Created);
        cut.WaitForAssertion(() => Assert.Contains("Created MOB Sprint 3.", _announced));
    }

    [Fact]
    public void P3_US2_AS5_Starting_proposes_today_and_an_end_two_weeks_later()
    {
        var cut = RenderBacklog();

        Sprint(cut, FakeBacklogService.PlannedId).QuerySelector("[data-testid=start-sprint]")!.Click();
        var dialog = cut.Find("[data-testid=start-sprint-dialog]");
        Assert.Equal("2026-09-27", cut.Find("#sprint-start").GetAttribute("value"));
        Assert.Equal("2026-10-11", cut.Find("#sprint-end").GetAttribute("value"));

        cut.Find("#sprint-start").Change("2026-10-05");
        Assert.Equal("2026-10-19", cut.Find("#sprint-end").GetAttribute("value"));
        cut.Find("#sprint-start-goal").Input("Ship refunds");
        cut.FindAll("[data-testid=start-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Start sprint").Click();

        Assert.Equal((FakeBacklogService.PlannedId, new DateOnly(2026, 10, 5), (DateOnly?)new DateOnly(2026, 10, 19), "Ship refunds"),
            Assert.Single(_sprints.Starts));
        Assert.NotNull(dialog);
    }

    [Fact]
    public void P3_US2_AS6_A_refused_start_explains_why_and_keeps_the_dialog_open()
    {
        _sprints.NextError = AppError.Rule(ErrorCodes.SprintAlreadyActive, "Only one sprint can be active at a time.");
        var cut = RenderBacklog();

        Sprint(cut, FakeBacklogService.PlannedId).QuerySelector("[data-testid=start-sprint]")!.Click();
        cut.FindAll("[data-testid=start-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Start sprint").Click();

        cut.WaitForAssertion(() => Assert.Contains("Only one sprint can be active at a time.",
            cut.Find("[data-testid=sprint-dialog-error]").TextContent, StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll("#sprint-start"));
    }

    [Fact]
    public void P3_US2_AS7_Completing_asks_where_the_open_items_go()
    {
        var cut = RenderBacklog();

        Sprint(cut, FakeBacklogService.ActiveId).QuerySelector("[data-testid=complete-sprint]")!.Click();

        Assert.Contains("1 item is done and 1 is open.", cut.Find("[data-testid=complete-sprint-dialog]").TextContent, StringComparison.Ordinal);
        Assert.Equal(["The backlog", "MOB Sprint 2"], cut.FindAll("#sprint-destination option").Select(o => o.TextContent.Trim()));
        cut.Find("#sprint-destination").Change(FakeBacklogService.PlannedId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cut.FindAll("[data-testid=complete-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Complete sprint").Click();

        Assert.Equal((FakeBacklogService.ActiveId, (long?)FakeBacklogService.PlannedId), Assert.Single(_sprints.Completions));
    }

    [Fact]
    public void P3_FR023_Deleting_a_planned_sprint_is_confirmed_first()
    {
        var cut = RenderBacklog();

        Sprint(cut, FakeBacklogService.PlannedId).QuerySelector("[data-testid=delete-sprint]")!.Click();

        Assert.Contains("Its 1 item returns to the backlog.", cut.Find("[data-testid=delete-sprint-dialog]").TextContent, StringComparison.Ordinal);
        Assert.Empty(_sprints.Deletes);
        cut.FindAll("[data-testid=delete-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Delete sprint").Click();
        Assert.Equal([FakeBacklogService.PlannedId], _sprints.Deletes);
    }

    [Fact]
    public void P3_FR023_Editing_a_sprint_saves_its_name_and_goal()
    {
        var cut = RenderBacklog();

        Sprint(cut, FakeBacklogService.ActiveId).QuerySelector("[data-testid=edit-sprint]")!.Click();
        Assert.Equal("MOB Sprint 1", cut.Find("#sprint-name").GetAttribute("value"));
        cut.Find("#sprint-name").Input("Refunds sprint");
        cut.FindAll("[data-testid=edit-sprint-dialog] button").Single(b => b.TextContent.Trim() == "Save").Click();

        Assert.Equal((FakeBacklogService.ActiveId, "Refunds sprint", "Ship card refunds"), Assert.Single(_sprints.Updates));
    }

    [Fact]
    public void P3_US2_AS10_Members_move_items_but_do_not_manage_sprints()
    {
        _backlog.View = _backlog.View with { CanManageSprints = false };

        var cut = RenderBacklog();

        Assert.Empty(cut.FindAll("[data-testid=create-sprint]"));
        Assert.Empty(cut.FindAll("[data-testid=edit-sprint], [data-testid=start-sprint], [data-testid=complete-sprint], [data-testid=delete-sprint]"));
        Assert.NotEmpty(cut.FindAll(".move-btn"));
    }

    [Fact]
    public void P3_US2_AS10_Viewers_read_the_backlog_but_change_nothing()
    {
        _backlog.View = _backlog.View with { CanContribute = false, CanManageSprints = false };

        var cut = RenderBacklog();

        Assert.Empty(cut.FindAll(".move-btn"));
        Assert.Empty(cut.FindAll("input.inline-input"));
        Assert.Empty(cut.FindAll("[data-testid=list-drop-end]"));
        Assert.All(cut.FindAll("[data-testid=backlog-row]"), row => Assert.Equal("false", row.GetAttribute("draggable")));
    }

    [Fact]
    public void Without_sprints_Project_Admins_are_offered_to_create_one_and_an_empty_backlog_says_so()
    {
        _backlog.View = _backlog.View with { Active = null, Planned = [], Backlog = new Page<BacklogRow>([], 0, 1, 100) };

        var cut = RenderBacklog();

        Assert.Contains("No sprints yet", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid=create-sprint]"));
        Assert.Contains("The backlog is empty", BacklogSection(cut).TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P3_FR029_Past_sprints_link_to_their_reports()
    {
        _backlog.View = _backlog.View with { Completed = [new CompletedSprintLink(1, "MOB Sprint 0", new DateOnly(2026, 9, 20))] };

        var cut = RenderBacklog();

        var link = cut.Find("[data-testid=past-sprints] a");
        Assert.Equal(("MOB Sprint 0", "spaces/MOB/sprints/1"), (link.TextContent.Trim(), link.GetAttribute("href")));
    }

    [Fact]
    public void A_project_without_a_backlog_shows_not_found()
    {
        var cut = RenderBacklog("WEB");

        Assert.Contains("Not found", cut.Find("h1").TextContent, StringComparison.Ordinal);
    }
}
