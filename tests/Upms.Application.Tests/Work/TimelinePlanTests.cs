using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The timeline as a plan: stages with their items, milestones, gates and dependencies (Phase 3 FR-042–FR-045,
/// FR-050, FR-051). WEB is amina's Stage-gate project.</summary>
public sealed class TimelinePlanTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    private static DateOnly Oct(int day) => new(2026, 10, day);

    private async Task<TimelineView> TimelineAsync() =>
        (await CallAsync<ITimelineService, Result<TimelineView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();

    private async Task ScheduleAsync(string key, DateOnly? start, DateOnly? due) =>
        (await EditAsync(key, new WorkItemEdit.Dates(start, due))).ValueOrThrow();

    [Fact]
    public async Task P3_US4_AS1_Stages_come_first_in_stage_order_with_their_items_and_progress()
    {
        await ScheduleAsync(Initiation, Oct(1), Oct(10));
        var charter = await TaskInAsync(Initiation, "Write the charter");      // WEB-5, scheduled
        var budget = await TaskInAsync(Initiation, "Agree the budget");        // WEB-6, unscheduled
        var loose = (await CreateAsync(WorkItemType.Task, "Order laptops")).Key; // WEB-7, in no stage
        await ScheduleAsync(charter, Oct(2), Oct(5));
        await ScheduleAsync(loose, Oct(3), Oct(4));

        var timeline = await TimelineAsync();

        Assert.Equal([Initiation, Planning, Execution, Closure], timeline.Groups.Select(g => g.Container.Key));
        var initiation = timeline.Groups[0];
        Assert.Equal((WorkItemType.Stage, (DateOnly?)Oct(1), (DateOnly?)Oct(10)), (initiation.Container.Type, initiation.Container.StartDate, initiation.Container.DueDate));
        Assert.Equal([charter], initiation.Rows.Select(r => r.Task.Key));
        Assert.Equal([budget], initiation.Unscheduled.Select(i => i.Key));
        Assert.Equal(new Progress(0, 2), initiation.Progress);
        Assert.False(timeline.Groups[1].Container.IsScheduled); // an undated stage keeps its group, without a bar
        Assert.Equal([loose], timeline.Rows.Select(r => r.Task.Key));
        Assert.DoesNotContain(timeline.Unscheduled.Items, i => i.Type == WorkItemType.Stage || i.Key == budget);
    }

    [Fact]
    public async Task P3_US4_AS2_A_milestone_is_scheduled_by_its_one_date()
    {
        var milestone = (await CreateAsync(WorkItemType.Milestone, "Planning sign-off", Planning)).Key;
        await ScheduleAsync(milestone, null, Oct(30));

        var row = Assert.Single((await TimelineAsync()).Groups.Single(g => g.Container.Key == Planning).Rows);
        Assert.Equal((milestone, WorkItemType.Milestone, (DateOnly?)Oct(30)), (row.Task.Key, row.Task.Type, row.Task.DueDate));

        var refused = await CallAsync<ITimelineService, Result<TimelineItem>>(s =>
            s.RescheduleAsync(milestone, Oct(28), Oct(30), row.Task.Version, Ct));
        Assert.Equal(ErrorCodes.InvalidDates, refused.Error!.Code);
        var moved = (await CallAsync<ITimelineService, Result<TimelineItem>>(s =>
            s.RescheduleAsync(milestone, null, new DateOnly(2026, 11, 1), row.Task.Version, Ct))).ValueOrThrow();
        Assert.Equal(new DateOnly(2026, 11, 1), moved.DueDate);
    }

    [Fact]
    public async Task P3_US4_AS3_Each_scheduled_stage_shows_its_gate_on_its_due_date()
    {
        await ScheduleAsync(Initiation, Oct(1), Oct(10));
        Assert.Equal(new GateMarker(Oct(10), GateState.NotRequested), (await TimelineAsync()).Groups[0].Gate);
        Assert.Null((await TimelineAsync()).Groups[1].Gate);

        await ApproveAsync(Initiation);

        Assert.Equal(GateState.Approved, (await TimelineAsync()).Groups[0].Gate!.State);
    }

    [Fact]
    public async Task P3_US4_AS6_Dependencies_between_shown_items_are_drawn_with_their_late_flags()
    {
        var api = (await CreateAsync(WorkItemType.Task, "Build the API")).Key;
        var app = (await CreateAsync(WorkItemType.Task, "Build the app")).Key;
        var undated = (await CreateAsync(WorkItemType.Task, "Write the manual")).Key;
        await ScheduleAsync(api, Oct(1), Oct(12));
        await ScheduleAsync(app, Oct(10), Oct(20));
        await CallAsync<IDependencyService, Result<DependencyList>>(s => s.AddAsync(app, api, Ct));
        await CallAsync<IDependencyService, Result<DependencyList>>(s => s.AddAsync(undated, api, Ct));

        var timeline = await TimelineAsync();

        Assert.Equal([new TimelineDependency(api, app, IsLate: true)], timeline.Dependencies);
        var waiting = timeline.Rows.Single(r => r.Task.Key == app).Task;
        Assert.Equal([new WaitingOn(api, IsLate: true)], waiting.WaitsFor);
    }
}

/// <summary>Epics on an agile project's timeline, and the sprint lane of a Scrum project (Phase 3 FR-042, FR-046).</summary>
public sealed class ScrumTimelineTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    private static DateOnly Oct(int day) => new(2026, 10, day);

    private async Task<TimelineView> TimelineAsync() =>
        (await CallAsync<ITimelineService, Result<TimelineView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();

    [Fact]
    public async Task P3_US4_AS1_Epics_are_ordered_by_first_date_then_due_date_and_key_and_undated_ones_follow()
    {
        var late = (await CreateAsync(WorkItemType.Epic, "Refunds")).Key;
        var early = (await CreateAsync(WorkItemType.Epic, "Statements")).Key;
        var undated = (await CreateAsync(WorkItemType.Epic, "Someday")).Key;
        (await EditAsync(late, new WorkItemEdit.Dates(Oct(10), Oct(20)))).ValueOrThrow();
        (await EditAsync(early, new WorkItemEdit.Dates(Oct(1), Oct(20)))).ValueOrThrow();

        Assert.Equal([early, late, undated], (await TimelineAsync()).Groups.Select(g => g.Container.Key));
    }

    [Fact]
    public async Task P3_US4_AS8_The_sprint_lane_shows_active_and_completed_sprints()
    {
        var first = await NewSprintAsync();
        await StartAsync(first.Id, Oct(5));
        await CompleteAsync(first.Id);
        var second = await NewSprintAsync();
        await StartAsync(second.Id, Oct(19));
        await NewSprintAsync(); // planned: not in the lane

        Assert.Equal(
        [
            new SprintBar("WEB Sprint 1", Oct(5), Oct(19), SprintState.Completed),
            new SprintBar("WEB Sprint 2", Oct(19), new DateOnly(2026, 11, 2), SprintState.Active),
        ], (await TimelineAsync()).Sprints);
    }
}
