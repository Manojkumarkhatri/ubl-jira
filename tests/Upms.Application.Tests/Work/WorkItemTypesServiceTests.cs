using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Types, epics and estimates in an agile project (Phase 3 US1; FR-010–FR-019). WEB is amina's Scrum project.</summary>
public sealed class WorkItemTypesServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected override ProjectTemplate Template => ProjectTemplate.Scrum;

    [Fact]
    public async Task P3_US1_AS4_An_epic_is_created_from_the_list_in_the_first_to_do_column_and_is_not_a_card()
    {
        var epic = await CreateAsync(WorkItemType.Epic, "Card refunds");

        Assert.Equal(("WEB-1", WorkItemType.Epic, ToDo), (epic.Key, epic.Type, epic.Status.Id));
        Assert.Equal(ProjectTemplate.Scrum, epic.Template);
        Assert.All((await BoardAsync()).Columns, c => Assert.Empty(c.Cards));
    }

    [Fact]
    public async Task P3_US1_AS5_An_epic_lists_its_items_and_shows_how_much_is_done()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");                  // WEB-1
        var story = await CreateAsync(WorkItemType.Story, "Refund to card");   // WEB-2
        var bug = await CreateAsync(WorkItemType.Bug, "Refund shows twice");   // WEB-3

        (await EditAsync(story.Key, new WorkItemEdit.Container("WEB-1"))).ValueOrThrow();
        (await EditAsync(bug.Key, new WorkItemEdit.Container("WEB-1"))).ValueOrThrow();
        (await EditAsync(story.Key, new WorkItemEdit.Status(Done))).ValueOrThrow();

        var epic = await RequireDetailsAsync("WEB-1");
        Assert.Equal(new Progress(1, 2), epic.AsContainer!.Progress);
        Assert.Equal(50, epic.AsContainer.Progress.Percent);
        Assert.Equal(["WEB-2", "WEB-3"], epic.AsContainer.Items.Items.Select(i => i.Key));
        Assert.Equal(new ContainerView("WEB-1", "Card refunds", WorkItemType.Epic, EpicColor.Purple), (await RequireDetailsAsync("WEB-3")).Container);
        var history = await HistoryAsync("WEB-3");
        Assert.Contains(history.Items, c => c.Field == WorkItemField.Container && c.OldValue is null && c.NewValue == "WEB-1 Card refunds");
    }

    [Fact]
    public async Task Items_can_be_created_straight_into_an_epic_and_a_container_without_items_shows_0_percent()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        Assert.Equal(new Progress(0, 0), (await RequireDetailsAsync("WEB-1")).AsContainer!.Progress);

        var story = await CreateAsync(WorkItemType.Story, "Refund to card", containerKey: "WEB-1");

        Assert.Equal("WEB-1", story.Container!.Key);
        Assert.Equal(new Progress(0, 1), (await RequireDetailsAsync("WEB-1")).AsContainer!.Progress);
    }

    [Fact]
    public async Task A_container_that_is_itself_done_shows_100_percent()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card", containerKey: "WEB-1");

        (await EditAsync("WEB-1", new WorkItemEdit.Status(Done))).ValueOrThrow();

        Assert.Equal(100, (await RequireDetailsAsync("WEB-1")).AsContainer!.Progress.Percent);
    }

    [Fact]
    public async Task P3_FR018_Completing_an_epic_with_open_items_is_allowed_with_a_warning_naming_them()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card", containerKey: "WEB-1");
        await CreateAsync(WorkItemType.Bug, "Refund shows twice", containerKey: "WEB-1");

        var done = await EditAsync("WEB-1", new WorkItemEdit.Status(Done));

        Assert.True(done.IsSuccess, done.Error?.Message);
        var warning = Assert.Single(done.Warnings);
        Assert.Contains("WEB-2", warning, StringComparison.Ordinal);
        Assert.Contains("WEB-3", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task P3_US1_AS6_A_bug_becomes_a_story_with_history_and_an_epic_cannot_change_type()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Bug, "Refund shows twice");
        Assert.Equal([WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug], (await RequireDetailsAsync("WEB-2")).TypeChoices);

        var changed = await EditAsync("WEB-2", new WorkItemEdit.Type(WorkItemType.Story));

        Assert.Equal(WorkItemType.Story, changed.ValueOrThrow().Type);
        Assert.Contains((await HistoryAsync("WEB-2")).Items, c => (c.Field, c.OldValue, c.NewValue) == (WorkItemField.Type, "Bug", "Story"));
        Assert.Empty((await RequireDetailsAsync("WEB-1")).TypeChoices);
        Assert.Equal(ErrorCodes.TypeChangeNotAllowed, (await EditAsync("WEB-1", new WorkItemEdit.Type(WorkItemType.Story))).Error!.Code);
    }

    [Fact]
    public async Task P3_US1_AS10_Deleting_an_epic_keeps_its_items_without_an_epic()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card", containerKey: "WEB-1");
        await CreateAsync(WorkItemType.Bug, "Refund shows twice", containerKey: "WEB-1");

        (await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-1", Ct))).ThrowIfFailed();

        foreach (var key in new[] { "WEB-2", "WEB-3" })
        {
            var item = await RequireDetailsAsync(key);
            Assert.Null(item.Container);
            Assert.Contains((await HistoryAsync(key)).Items,
                c => (c.Field, c.OldValue, c.NewValue) == (WorkItemField.Container, "WEB-1 Card refunds", null));
        }

        Assert.Equal(ErrorKind.NotFound, (await DetailsAsync("WEB-1")).Error!.Kind);
    }

    [Fact]
    public async Task P3_FR014_Stories_take_an_estimate_and_epics_do_not()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card");

        Assert.Equal(2.5m, (await EditAsync("WEB-2", new WorkItemEdit.Estimate(2.5m))).ValueOrThrow().Estimate);
        Assert.Contains((await HistoryAsync("WEB-2")).Items, c => (c.Field, c.NewValue) == (WorkItemField.Estimate, "2.5"));

        var refused = await EditAsync("WEB-1", new WorkItemEdit.Estimate(3));
        Assert.Equal(ErrorKind.Validation, refused.Error!.Kind);
        Assert.Contains("Estimate", refused.Error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task P3_FR007_A_type_the_template_does_not_offer_is_refused()
    {
        var stage = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Stage, "Planning"), Ct));
        var milestone = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Milestone, "Launch"), Ct));

        Assert.Equal(ErrorCodes.TypeNotAllowed, stage.Error!.Code);
        Assert.Equal(ErrorCodes.TypeNotAllowed, milestone.Error!.Code);
    }

    [Fact]
    public async Task P3_FR008_Only_an_epic_of_the_same_project_can_hold_an_item()
    {
        await CreateAsync(WorkItemType.Story, "Refund to card");       // WEB-1
        await CreateAsync(WorkItemType.Bug, "Refund shows twice");     // WEB-2

        Assert.Equal(ErrorCodes.InvalidContainer, (await EditAsync("WEB-2", new WorkItemEdit.Container("WEB-1"))).Error!.Code);
        Assert.Equal(ErrorCodes.InvalidContainer, (await EditAsync("WEB-2", new WorkItemEdit.Container("NOPE-1"))).Error!.Code);
    }

    [Fact]
    public async Task The_epic_choices_list_the_projects_epics()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card");
        await CreateAsync(WorkItemType.Epic, "Statements");

        var choices = (await CallAsync<IWorkItemService, Result<IReadOnlyList<ContainerOption>>>(s => s.ListContainersAsync("WEB", Ct))).ValueOrThrow();

        Assert.Equal(["WEB-1", "WEB-3"], choices.Select(c => c.Key));
        Assert.All(choices, c => Assert.Equal((WorkItemType.Epic, false), (c.Type, c.IsLocked)));
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict()
    {
        var story = await CreateAsync(WorkItemType.Story, "Refund to card");
        (await EditAsync(story.Key, new WorkItemEdit.Estimate(3))).ValueOrThrow();

        var stale = await EditAsync(story.Key, new WorkItemEdit.Type(WorkItemType.Bug), story.Version);

        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(WorkItemType.Story, (await RequireDetailsAsync(story.Key)).Type);
    }
}

/// <summary>Stages and milestones in a Stage-gate project (Phase 3 US1; FR-003, FR-010, FR-012). WEB is amina's
/// Stage-gate project, so it starts with the stages WEB-1 to WEB-4.</summary>
public sealed class StageGateItemsTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    protected override ProjectTemplate Template => ProjectTemplate.StageGate;

    [Fact]
    public async Task P3_US1_AS7_A_milestone_in_a_stage_has_one_date_no_sub_tasks_and_no_card()
    {
        var milestone = await CreateAsync(WorkItemType.Milestone, "Planning sign-off", containerKey: "WEB-2");

        Assert.Equal(("WEB-5", "WEB-2"), (milestone.Key, milestone.Container!.Key));
        var dated = (await EditAsync(milestone.Key, new WorkItemEdit.Dates(null, new DateOnly(2026, 10, 30)))).ValueOrThrow();
        Assert.Equal((null, new DateOnly(2026, 10, 30)), (dated.StartDate, dated.DueDate));

        var twoDates = await EditAsync(milestone.Key, new WorkItemEdit.Dates(new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 30)));
        Assert.Equal(ErrorCodes.InvalidDates, twoDates.Error!.Code);
        var subtask = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync(milestone.Key, "Sign", Ct));
        Assert.Equal(ErrorCodes.NoSubtasks, subtask.Error!.Code);
        Assert.All((await BoardAsync()).Columns, c => Assert.Empty(c.Cards));
    }

    [Fact]
    public async Task A_task_goes_into_a_stage_and_the_board_shows_it()
    {
        var task = await CreateAsync(WorkItemType.Task, "Finalize vendor contract", containerKey: "WEB-2");

        Assert.Equal("WEB-2", task.Container!.Key);
        Assert.Equal([task.Key], (await BoardAsync()).Columns[0].Cards.Select(c => c.Key));
        Assert.Equal(new Progress(0, 1), (await RequireDetailsAsync("WEB-2")).AsContainer!.Progress);
    }

    [Fact]
    public async Task Milestones_do_not_count_towards_progress()
    {
        await CreateAsync(WorkItemType.Task, "Finalize vendor contract", containerKey: "WEB-2");
        await CreateAsync(WorkItemType.Milestone, "Planning sign-off", containerKey: "WEB-2");

        Assert.Equal(new Progress(0, 1), (await RequireDetailsAsync("WEB-2")).AsContainer!.Progress);
    }

    [Fact]
    public async Task The_stage_choices_are_the_stages_in_stage_order()
    {
        var choices = (await CallAsync<IWorkItemService, Result<IReadOnlyList<ContainerOption>>>(s => s.ListContainersAsync("WEB", Ct))).ValueOrThrow();

        Assert.Equal(["Initiation", "Planning", "Execution", "Closure"], choices.Select(c => c.Title));
    }

    [Fact]
    public async Task Stories_bugs_and_epics_are_not_used_in_a_Stage_gate_project()
    {
        foreach (var type in new[] { WorkItemType.Story, WorkItemType.Bug, WorkItemType.Epic })
        {
            var result = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.CreateAsync("WEB", new NewWorkItem(type, "x"), Ct));
            Assert.Equal(ErrorCodes.TypeNotAllowed, result.Error!.Code);
        }
    }

    [Fact]
    public async Task Only_Project_Admins_and_administrators_create_stages_and_a_new_stage_goes_last()
    {
        ActAs(await MemberAsync("bilal"));
        var byMember = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Stage, "Warranty"), Ct));
        Assert.Equal(ErrorKind.Forbidden, byMember.Error!.Kind);

        ActAs(Owner);
        await CreateAsync(WorkItemType.Stage, "Warranty");

        var stages = await QueryAsync(db => db.WorkItems.AsNoTracking()
            .Where(w => w.Type == WorkItemType.Stage).OrderBy(w => w.Rank).ThenBy(w => w.Id).Select(w => w.Title).ToListAsync(Ct));
        Assert.Equal(["Initiation", "Planning", "Execution", "Closure", "Warranty"], stages);
    }

    [Fact]
    public async Task Stage_gate_tasks_have_no_estimate()
    {
        var task = await CreateAsync(WorkItemType.Task, "Finalize vendor contract");

        Assert.Empty(task.TypeChoices);
        Assert.Equal(ErrorKind.Validation, (await EditAsync(task.Key, new WorkItemEdit.Estimate(3))).Error!.Kind);
    }

    [Fact]
    public async Task Members_can_add_tasks_but_not_stages_to_a_stage()
    {
        ActAs(await MemberAsync("bilal", ProjectRole.Member));

        var task = await CreateAsync(WorkItemType.Task, "Collect quotes", containerKey: "WEB-1");

        Assert.Equal("WEB-1", task.Container!.Key);
    }
}
