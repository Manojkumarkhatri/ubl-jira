using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The backlog of a Scrum project: one order for the backlog, the sprints and the board, and cards moving
/// between them (Phase 3 FR-021, FR-022, FR-024, FR-026).</summary>
public sealed class BacklogServiceTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    [Fact]
    public async Task P3_US2_AS1_The_backlog_lists_the_sprints_with_totals_then_open_cards_by_rank()
    {
        var epic = (await CreateAsync(WorkItemType.Epic, "Card refunds")).Key;     // WEB-1: not listed
        var refund = await StoryAsync("Refund to card", 3);                         // WEB-2
        var form = await StoryAsync("Refund form", 5);                              // WEB-3
        var email = await StoryAsync("Refund email", type: WorkItemType.Task);      // WEB-4: no estimate
        var twice = await StoryAsync("Refund shows twice", 2, WorkItemType.Bug);    // WEB-5
        var shipped = await StoryAsync("Old refund page");                         // WEB-6: done, not in a sprint
        (await EditAsync(refund, new WorkItemEdit.Container(epic))).ValueOrThrow();
        await AddSubtaskAsync(refund, "Write the refund rules");                    // WEB-7: travels with WEB-2
        await MarkDoneAsync(shipped);

        var first = await NewSprintAsync();
        var second = await NewSprintAsync();
        await MoveAsync(form, Into(first));
        await MoveAsync(email, Into(first));
        await MoveAsync(twice, Into(second));
        await StartAsync(first.Id);
        await MarkDoneAsync(form);

        var view = await BacklogAsync();

        Assert.Equal(("WEB", true, true), (view.ProjectKey, view.CanContribute, view.CanManageSprints));
        var active = view.Active!;
        Assert.Equal(("WEB Sprint 1", SprintState.Active), (active.Name, active.State));
        Assert.Equal([form, email], KeysOf(active.Items));
        Assert.Equal(new SprintTotals(2, 5, 1, 5, 0), active.Totals);
        var planned = Assert.Single(view.Planned);
        Assert.Equal(("WEB Sprint 2", SprintState.Planned), (planned.Name, planned.State));
        Assert.Equal([twice], KeysOf(planned.Items));
        Assert.Equal(new SprintTotals(1, 2, 0, 0, 2), planned.Totals);

        var row = Assert.Single(view.Backlog.Items);
        Assert.Equal((refund, WorkItemType.Story, 3m, 1), (row.Key, row.Type, row.Estimate, row.SubtaskCount));
        Assert.Equal("WEB-1", row.Epic?.Key);
        Assert.Empty(view.Completed);
    }

    [Fact]
    public async Task The_backlog_shows_100_cards_at_a_time()
    {
        for (var n = 1; n <= 101; n++)
        {
            await StoryAsync($"Story {n}");
        }

        var first = await BacklogAsync();
        var second = await BacklogAsync(page: 2);

        Assert.Equal((100, 101), (first.Backlog.Items.Count, first.Backlog.TotalCount));
        Assert.Equal(["WEB-101"], KeysOf(second.Backlog.Items));
    }

    [Fact]
    public async Task P3_US2_AS2_Reordering_keeps_one_order_that_the_board_uses_too()
    {
        var a = await StoryAsync("A");
        var b = await StoryAsync("B");
        var c = await StoryAsync("C");
        var d = await StoryAsync("D");

        await MoveAsync(d, BacklogTarget.ToBacklog, BacklogPlacement.AtTop);
        Assert.Equal([d, a, b, c], await BacklogKeysAsync());
        await MoveAsync(a, BacklogTarget.ToBacklog, BacklogPlacement.AtBottom);
        Assert.Equal([d, b, c, a], await BacklogKeysAsync());
        await MoveAsync(c, BacklogTarget.ToBacklog, BacklogPlacement.OneUp);
        Assert.Equal([d, c, b, a], await BacklogKeysAsync());
        await MoveAsync(d, BacklogTarget.ToBacklog, BacklogPlacement.OneDown);
        Assert.Equal([c, d, b, a], await BacklogKeysAsync());
        await MoveAsync(a, BacklogTarget.ToBacklog, new BacklogPlacement.Before(c));
        Assert.Equal([a, c, d, b], await BacklogKeysAsync());

        // The board of a sprint holding all four reads the same order.
        var sprint = await NewSprintAsync();
        foreach (var key in new[] { a, c, d, b })
        {
            await MoveAsync(key, Into(sprint));
        }

        await StartAsync(sprint.Id);
        Assert.Equal([a, c, d, b], await KeysInAsync(ToDo));
        Assert.Contains((await HistoryAsync(a)).Items, h => h.Field == WorkItemField.Rank && h.Note == $"moved above {c}");
    }

    [Fact]
    public async Task Moving_the_top_card_up_or_the_last_card_down_changes_nothing()
    {
        var a = await StoryAsync("A");
        var b = await StoryAsync("B");

        await MoveAsync(a, BacklogTarget.ToBacklog, BacklogPlacement.OneUp);
        await MoveAsync(b, BacklogTarget.ToBacklog, BacklogPlacement.OneDown);

        Assert.Equal([a, b], await BacklogKeysAsync());
        Assert.DoesNotContain((await HistoryAsync(a)).Items, h => h.Field == WorkItemField.Rank);
    }

    [Fact]
    public async Task P3_US2_AS3_Cards_move_into_between_and_out_of_sprints_with_history()
    {
        var a = await StoryAsync("A");
        var b = await StoryAsync("B");
        var c = await StoryAsync("C");
        var first = await NewSprintAsync();
        var second = await NewSprintAsync();

        await MoveAsync(b, Into(first));
        await MoveAsync(a, Into(first), BacklogPlacement.AtBottom);
        Assert.Equal([b, a], KeysOf((await SprintAsync(first.Id)).Items));
        Assert.Equal([c], await BacklogKeysAsync());

        await MoveAsync(b, Into(second));
        await MoveAsync(a, BacklogTarget.ToBacklog);
        Assert.Empty((await SprintAsync(first.Id)).Items);
        Assert.Equal([b], KeysOf((await SprintAsync(second.Id)).Items));
        Assert.Equal([a, c], await BacklogKeysAsync());

        var sprintChanges = (await HistoryAsync(b)).Items.Where(h => h.Field == WorkItemField.Sprint).Select(h => (h.OldValue, h.NewValue));
        Assert.Equal([(null, "WEB Sprint 1"), ("WEB Sprint 1", "WEB Sprint 2")], sprintChanges);
        Assert.Equal("WEB Sprint 2", (await RequireDetailsAsync(b)).Sprint?.Name);
        Assert.Null((await RequireDetailsAsync(a)).Sprint);
    }

    [Fact]
    public async Task P3_US2_AS4_Sub_tasks_follow_their_parent_and_cannot_move_on_their_own()
    {
        var story = await StoryAsync("Refund to card");
        var subtask = (await AddSubtaskAsync(story, "Write the refund rules")).Subtasks.Items[0].Key;
        await AddSubtaskAsync(story, "Test the refund rules");
        var sprint = await NewSprintAsync();

        await MoveAsync(story, Into(sprint));

        var row = Assert.Single((await SprintAsync(sprint.Id)).Items);
        Assert.Equal(2, row.SubtaskCount);
        Assert.Equal(ErrorCodes.TypeNotAllowed, (await TryMoveAsync(subtask, BacklogTarget.ToBacklog)).Error!.Code);
    }

    [Fact]
    public async Task Epics_are_not_moved_into_sprints()
    {
        var epic = (await CreateAsync(WorkItemType.Epic, "Card refunds")).Key;
        var sprint = await NewSprintAsync();

        Assert.Equal(ErrorCodes.TypeNotAllowed, (await TryMoveAsync(epic, Into(sprint))).Error!.Code);
    }

    [Fact]
    public async Task P3_FR028_A_completed_sprint_keeps_its_done_cards_and_takes_no_more()
    {
        var done = await StoryAsync("Done in sprint 1");
        var open = await StoryAsync("Still open");
        var sprint = await NewSprintAsync();
        await MoveAsync(done, Into(sprint));
        await StartAsync(sprint.Id);
        await MarkDoneAsync(done);
        await CompleteAsync(sprint.Id);

        Assert.Equal(ErrorCodes.SprintCompleted, (await TryMoveAsync(done, BacklogTarget.ToBacklog)).Error!.Code);
        Assert.Equal(ErrorCodes.SprintCompleted, (await TryMoveAsync(open, Into(sprint))).Error!.Code);
        Assert.Equal(sprint.Id, await SprintIdOfAsync(done));
        var completed = Assert.Single((await BacklogAsync()).Completed);
        Assert.Equal((1, "WEB Sprint 1", (DateOnly?)October5.AddDays(14)), (completed.Number, completed.Name, completed.EndDate));
    }

    [Fact]
    public async Task A_card_reopened_after_its_sprint_completed_is_back_in_the_backlog()
    {
        var story = await StoryAsync("Refund to card");
        var sprint = await NewSprintAsync();
        await MoveAsync(story, Into(sprint));
        await StartAsync(sprint.Id);
        await MarkDoneAsync(story);
        await CompleteAsync(sprint.Id);
        Assert.Empty(await BacklogKeysAsync());

        await ReopenAsync(story);

        Assert.Equal([story], await BacklogKeysAsync());
        var next = await NewSprintAsync();
        await MoveAsync(story, Into(next));
        Assert.Equal([story], KeysOf((await SprintAsync(next.Id)).Items));
    }

    [Fact]
    public async Task A_stale_version_returns_the_current_row()
    {
        var story = await StoryAsync("Refund to card");
        var stale = (await RequireDetailsAsync(story)).Version;
        (await EditAsync(story, new WorkItemEdit.Title("Refund to the card"))).ValueOrThrow();

        var result = await TryMoveAsync(story, BacklogTarget.ToBacklog, BacklogPlacement.AtTop, stale);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("Refund to the card", Assert.IsType<BacklogRow>(result.Error.Current).Title);
    }

    [Fact]
    public async Task P3_US2_AS10_Viewers_read_the_backlog_and_Members_move_cards()
    {
        var story = await StoryAsync("Refund to card");
        var sprint = await NewSprintAsync();
        var viewer = await MemberAsync("vera", ProjectRole.Viewer);
        var member = await MemberAsync("musa");

        ActAs(viewer);
        var view = await BacklogAsync();
        Assert.Equal((false, false), (view.CanContribute, view.CanManageSprints));
        Assert.Equal(ErrorKind.Forbidden, (await TryMoveAsync(story, Into(sprint))).Error!.Kind);

        ActAs(member);
        Assert.Equal((true, false), ((await BacklogAsync()).CanContribute, (await BacklogAsync()).CanManageSprints));
        await MoveAsync(story, Into(sprint));
        Assert.Equal([story], KeysOf((await SprintAsync(sprint.Id)).Items));
    }

    [Fact]
    public async Task Moving_into_or_out_of_the_active_sprint_is_recorded_for_its_report()
    {
        var committed = await StoryAsync("Committed", 3);
        var added = await StoryAsync("Added", 5);
        var sprint = await NewSprintAsync();
        await MoveAsync(committed, Into(sprint));
        await StartAsync(sprint.Id);

        await MoveAsync(added, Into(sprint));
        await MoveAsync(committed, BacklogTarget.ToBacklog);

        Assert.Equal(
            [(committed, SprintScopeKind.Committed, 3m), (added, SprintScopeKind.Added, 5m), (committed, SprintScopeKind.Removed, 3m)],
            await ScopeEventsAsync(sprint.Id));
    }

    [Fact]
    public async Task Kanban_and_Stage_gate_projects_have_no_backlog()
    {
        await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Core Banking", "CBU", null, ProjectTemplate.StageGate, Ct));
        await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Intranet", "INT", null, ProjectTemplate.Kanban, Ct));

        Assert.Equal(ErrorKind.NotFound, (await TryBacklogAsync(projectKey: "CBU")).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await TryBacklogAsync(projectKey: "INT")).Error!.Kind);
    }
}

/// <summary>A Kanban project switched to Scrum lists its open cards in the backlog column by column, keeping each
/// column's order (Phase 3 US1 AS9, completed here where the backlog exists).</summary>
public sealed class SwitchToScrumBacklogTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    [Fact]
    public async Task P3_US1_AS9_The_backlog_follows_the_board_column_by_column()
    {
        var a = await AddAsync(ToDo, "A");
        var b = await AddAsync(InProgress, "B");
        var c = await AddAsync(ToDo, "C");
        var d = await AddAsync(Done, "D");
        var e = await AddAsync(InProgress, "E");
        await MoveAsync(c, ToDo, CardPlacement.AtTop);
        var version = (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("WEB", Ct))).Value!.DetailsVersion;

        (await CallAsync<IProjectService, Result<ProjectDetails>>(s => s.SwitchTemplateAsync("WEB", ProjectTemplate.Scrum, version, Ct)))
            .ValueOrThrow();

        var backlog = (await CallAsync<IBacklogService, Result<BacklogView>>(s => s.GetAsync("WEB", new(1), Ct))).ValueOrThrow();
        Assert.Equal([c.Key, a.Key, b.Key, e.Key], backlog.Backlog.Items.Select(r => r.Key));
        Assert.DoesNotContain(d.Key, backlog.Backlog.Items.Select(r => r.Key));
    }
}
