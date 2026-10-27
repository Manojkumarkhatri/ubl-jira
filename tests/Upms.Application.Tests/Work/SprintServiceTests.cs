using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Creating, starting, completing and deleting sprints, and their reports (Phase 3 FR-023, FR-025, FR-028,
/// FR-029).</summary>
public sealed class SprintServiceTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    private Task<Result<SprintSection>> TryUpdateAsync(SprintSection sprint, string name, string? goal, byte[]? version = null) =>
        CallAsync<ISprintService, Result<SprintSection>>(s => s.UpdateAsync(sprint.Id, name, goal, version ?? sprint.Version, Ct));

    private async Task<Result> TryDeleteAsync(long id, byte[]? version = null)
    {
        version ??= (await SprintAsync(id)).Version;
        return await CallAsync<ISprintService, Result>(s => s.DeleteAsync(id, version, Ct));
    }

    [Fact]
    public async Task P3_US2_AS3_Sprints_are_numbered_and_named_after_the_project()
    {
        var first = await NewSprintAsync();
        var second = await NewSprintAsync();

        Assert.Equal((1, "WEB Sprint 1", SprintState.Planned), (first.Number, first.Name, first.State));
        Assert.Equal((2, "WEB Sprint 2"), (second.Number, second.Name));
        Assert.Equal(new SprintTotals(0, 0, 0, 0, 0), first.Totals);
    }

    [Fact]
    public async Task P3_US2_AS5_Starting_defaults_the_end_and_records_what_was_committed()
    {
        var a = await StoryAsync("A", 3);
        var b = await StoryAsync("B");
        await AddSubtaskAsync(a, "A's part");
        var sprint = await NewSprintAsync();
        await MoveAsync(a, Into(sprint));
        await MoveAsync(b, Into(sprint));

        var started = await StartAsync(sprint.Id, October5, goal: "Ship refunds");

        Assert.Equal((SprintState.Active, (DateOnly?)October5, (DateOnly?)new DateOnly(2026, 10, 19), "Ship refunds"),
            (started.State, started.StartDate, started.EndDate, started.Goal));
        Assert.Equal([(a, SprintScopeKind.Committed, 3m), (b, SprintScopeKind.Committed, null)], await ScopeEventsAsync(sprint.Id));
        Assert.Equal("WEB Sprint 1", (await BacklogAsync()).Active?.Name);
    }

    [Fact]
    public async Task Starting_refuses_an_end_before_the_start()
    {
        var sprint = await NewSprintAsync();

        var result = await TryStartAsync(sprint.Id, October5, October5.AddDays(-1));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.True(result.Error.FieldErrors!.ContainsKey("EndDate"));
        Assert.Null((await BacklogAsync()).Active);
    }

    [Fact]
    public async Task P3_US2_AS6_Only_one_sprint_is_active_at_a_time()
    {
        var first = await NewSprintAsync();
        var second = await NewSprintAsync();
        await StartAsync(first.Id);

        var refused = await TryStartAsync(second.Id);

        Assert.Equal(ErrorCodes.SprintAlreadyActive, refused.Error!.Code);
        Assert.Equal(SprintState.Planned, (await SprintAsync(second.Id)).State);
    }

    [Fact]
    public async Task P3_US2_AS6_Two_sprints_started_at_the_same_moment_leave_one_active()
    {
        var first = await NewSprintAsync();
        var second = await NewSprintAsync();

        var results = await Task.WhenAll(
            CallAsync<ISprintService, Result<SprintSection>>(s => s.StartAsync(first.Id, October5, null, null, first.Version, Ct)),
            CallAsync<ISprintService, Result<SprintSection>>(s => s.StartAsync(second.Id, October5, null, null, second.Version, Ct)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(ErrorCodes.SprintAlreadyActive, Assert.Single(results, r => !r.IsSuccess).Error!.Code);
    }

    [Fact]
    public async Task P3_US2_AS7_Completing_keeps_done_cards_and_sends_open_ones_to_the_backlog()
    {
        var keys = new List<string>();
        for (var n = 1; n <= 7; n++)
        {
            keys.Add(await StoryAsync($"Story {n}", n));
        }

        var sprint = await NewSprintAsync();
        foreach (var key in keys)
        {
            await MoveAsync(key, Into(sprint));
        }

        await StartAsync(sprint.Id);
        foreach (var key in keys.Take(4))
        {
            await MarkDoneAsync(key);
        }

        var completed = await CompleteAsync(sprint.Id);

        Assert.Equal(SprintState.Completed, completed.State);
        Assert.Equal(keys.Skip(4), await BacklogKeysAsync());
        foreach (var key in keys.Take(4))
        {
            Assert.Equal(sprint.Id, await SprintIdOfAsync(key));
        }

        Assert.Contains((await HistoryAsync(keys[5])).Items, h => h.Field == WorkItemField.Sprint && h.OldValue == "WEB Sprint 1" && h.NewValue == null);
        var events = await ScopeEventsAsync(sprint.Id);
        Assert.Equal(4, events.Count(e => e.Kind == SprintScopeKind.Completed));
        Assert.Equal(3, events.Count(e => e.Kind == SprintScopeKind.NotCompleted));
        Assert.Equal(ErrorCodes.SprintCompleted, (await TryUpdateAsync(completed, "Renamed", null)).Error!.Code);
    }

    [Fact]
    public async Task Completing_can_send_open_cards_to_a_planned_sprint()
    {
        var open = await StoryAsync("Open");
        var sprint = await NewSprintAsync();
        var next = await NewSprintAsync();
        await MoveAsync(open, Into(sprint));
        await StartAsync(sprint.Id);

        await CompleteAsync(sprint.Id, next.Id);

        Assert.Equal([open], KeysOf((await SprintAsync(next.Id)).Items));
        Assert.Contains((await HistoryAsync(open)).Items, h => h.Field == WorkItemField.Sprint && h.OldValue == "WEB Sprint 1" && h.NewValue == "WEB Sprint 2");
    }

    [Fact]
    public async Task Only_the_active_sprint_can_be_completed()
    {
        var planned = await NewSprintAsync();

        Assert.Equal(ErrorCodes.SprintNotActive, (await TryCompleteAsync(planned.Id)).Error!.Code);
    }

    [Fact]
    public async Task P3_US2_AS8_The_report_groups_the_sprint_s_scope_and_never_changes()
    {
        var keys = new List<string>();
        for (var n = 1; n <= 6; n++)
        {
            keys.Add(await StoryAsync($"Story {n}", n));   // estimates 1 to 6
        }

        var late = await StoryAsync("Added later", 8);
        var sprint = await NewSprintAsync();
        foreach (var key in keys)
        {
            await MoveAsync(key, Into(sprint));
        }

        await StartAsync(sprint.Id, goal: "Ship refunds");
        await MoveAsync(late, Into(sprint));                                          // added: 8
        await MoveAsync(keys[5], BacklogTarget.ToBacklog);                             // removed: 6
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(keys[4], Ct))).IsSuccess);   // removed: 5 (deleted)
        foreach (var key in keys.Take(3))
        {
            await MarkDoneAsync(key);                                                  // completed: 1 + 2 + 3
        }

        await CompleteAsync(sprint.Id);                                                // not completed: 4 and 8

        var report = (await TryReportAsync(1)).ValueOrThrow();
        Assert.Equal(("WEB Sprint 1", "Ship refunds", October5, October5.AddDays(14)), (report.Name, report.Goal, report.StartDate, report.EndDate));
        Assert.Equal((6, 21m), (report.Committed.Items.Count, report.Committed.EstimateTotal));
        Assert.Equal([late], report.Added.Items.Select(i => i.Key));
        Assert.Equal(8m, report.Added.EstimateTotal);
        Assert.Equal((2, 11m), (report.Removed.Items.Count, report.Removed.EstimateTotal));
        Assert.True(report.Removed.Items.Single(i => i.Key == keys[4]).IsDeleted);
        Assert.Equal((3, 6m), (report.Completed.Items.Count, report.Completed.EstimateTotal));
        Assert.Equal((2, 12m), (report.NotCompleted.Items.Count, report.NotCompleted.EstimateTotal));

        // Later edits do not change it.
        (await EditAsync(keys[0], new WorkItemEdit.Estimate(13))).ValueOrThrow();
        await ReopenAsync(keys[1]);
        Assert.Equivalent(report, (await TryReportAsync(1)).ValueOrThrow());
    }

    [Fact]
    public async Task Only_completed_sprints_have_reports()
    {
        await StartAsync((await NewSprintAsync()).Id);

        Assert.Equal(ErrorKind.NotFound, (await TryReportAsync(1)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await TryReportAsync(9)).Error!.Kind);
    }

    [Fact]
    public async Task P3_FR023_Planned_and_active_sprints_are_renamed_and_given_goals()
    {
        var sprint = await NewSprintAsync();

        var renamed = (await TryUpdateAsync(sprint, "Refunds sprint", "Ship card refunds")).ValueOrThrow();
        Assert.Equal(("Refunds sprint", "Ship card refunds"), (renamed.Name, renamed.Goal));

        var invalid = await TryUpdateAsync(renamed, "  ", null);
        Assert.True(invalid.Error!.FieldErrors!.ContainsKey("Name"));

        var active = await StartAsync(sprint.Id);
        Assert.Equal("Still refunds", (await TryUpdateAsync(active, "Still refunds", null)).ValueOrThrow().Name);
    }

    [Fact]
    public async Task P3_FR023_Deleting_a_planned_sprint_returns_its_cards_to_the_backlog_in_order()
    {
        var a = await StoryAsync("A");
        var b = await StoryAsync("B");
        var c = await StoryAsync("C");
        var sprint = await NewSprintAsync();
        await MoveAsync(a, Into(sprint));
        await MoveAsync(c, Into(sprint));

        Assert.True((await TryDeleteAsync(sprint.Id)).IsSuccess);

        var view = await BacklogAsync();
        Assert.Empty(view.Planned);
        Assert.Equal([a, b, c], KeysOf(view.Backlog.Items));
        Assert.Contains((await HistoryAsync(c)).Items, h => h.Field == WorkItemField.Sprint && h.OldValue == "WEB Sprint 1" && h.NewValue == null);
    }

    [Fact]
    public async Task An_active_sprint_cannot_be_deleted()
    {
        var sprint = await NewSprintAsync();
        await StartAsync(sprint.Id);

        Assert.Equal(ErrorCodes.SprintNotPlanned, (await TryDeleteAsync(sprint.Id)).Error!.Code);
    }

    [Fact]
    public async Task A_stale_version_returns_the_current_sprint()
    {
        var sprint = await NewSprintAsync();
        (await TryUpdateAsync(sprint, "Refunds sprint", null)).ValueOrThrow();

        var result = await TryUpdateAsync(sprint, "Another name", null);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("Refunds sprint", Assert.IsType<SprintSection>(result.Error.Current).Name);
    }

    [Fact]
    public async Task P3_US2_AS10_Only_Project_Admins_and_administrators_manage_sprints()
    {
        var sprint = await NewSprintAsync();
        var member = await MemberAsync("musa");
        var viewer = await MemberAsync("vera", ProjectRole.Viewer);
        var outsider = await Data.UserAsync("omar");
        var admin = await Data.AdministratorAsync();

        foreach (var (user, kind) in new[] { (member, ErrorKind.Forbidden), (viewer, ErrorKind.Forbidden), (outsider, ErrorKind.NotFound) })
        {
            ActAs(user);
            Assert.Equal(kind, (await TryCreateSprintAsync()).Error!.Kind);
            Assert.Equal(kind, (await TryUpdateAsync(sprint, "Renamed", null)).Error!.Kind);
            Assert.Equal(kind, (await CallAsync<ISprintService, Result<SprintSection>>(s =>
                s.StartAsync(sprint.Id, October5, null, null, sprint.Version, Ct))).Error!.Kind);
            Assert.Equal(kind, (await CallAsync<ISprintService, Result<SprintSection>>(s =>
                s.CompleteAsync(sprint.Id, null, sprint.Version, Ct))).Error!.Kind);
            Assert.Equal(kind, (await CallAsync<ISprintService, Result>(s => s.DeleteAsync(sprint.Id, sprint.Version, Ct))).Error!.Kind);
        }

        ActAs(admin);
        Assert.Equal("WEB Sprint 2", (await TryCreateSprintAsync()).ValueOrThrow().Name);
    }

    [Fact]
    public async Task Viewers_read_sprint_reports()
    {
        var sprint = await NewSprintAsync();
        await StartAsync(sprint.Id);
        await CompleteAsync(sprint.Id);
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        Assert.Equal("WEB Sprint 1", (await TryReportAsync(1)).ValueOrThrow().Name);
    }

    [Fact]
    public async Task Kanban_projects_have_no_sprints()
    {
        await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Intranet", "INT", null, ProjectTemplate.Kanban, Ct));

        Assert.Equal(ErrorKind.NotFound, (await TryCreateSprintAsync("INT")).Error!.Kind);
    }
}
