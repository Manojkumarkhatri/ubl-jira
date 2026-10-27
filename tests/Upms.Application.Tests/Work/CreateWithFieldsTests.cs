using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>The Create form's service path: any work type with every field, in one change (Jira parity pass US1, FR-003–FR-007;
/// research R11). WEB is amina's Kanban space; omar and bilal are Members.</summary>
public sealed class CreateWithFieldsTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _omar = null!;
    private User _bilal = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _omar = await MemberAsync("omar");
        _bilal = await MemberAsync("bilal");
        ActAs(Owner);
    }

    private Task<Result<WorkItemDetails>> TryCreateAsync(NewWorkItem item, string spaceKey = "WEB") =>
        CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.CreateAsync(spaceKey, item, Ct));

    private Task<int> CountAsync() => QueryAsync(db => db.WorkItems.IgnoreQueryFilters().CountAsync(Ct));

    private Task<List<WorkItemChange>> ChangesAsync(string key) =>
        QueryAsync(db => db.WorkItemChanges.AsNoTracking()
            .Where(c => db.WorkItems.Any(w => w.Key == key && w.Id == c.WorkItemId))
            .OrderBy(c => c.Id)
            .ToListAsync(Ct));

    [Fact]
    public async Task JP_US1_AS2_Every_field_is_saved_and_the_history_shows_one_change()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds"); // WEB-1
        var start = new DateOnly(2026, 10, 12);
        var due = new DateOnly(2026, 10, 23);

        var created = (await TryCreateAsync(new NewWorkItem(WorkItemType.Story, "Refund to card", "WEB-1")
        {
            Description = Mentioning("Check with ", _bilal),
            StatusId = InProgress,
            AssigneeId = _omar.Id,
            Priority = Priority.High,
            Labels = ["payments", "Q4"],
            StartDate = start,
            DueDate = due,
            Estimate = 3,
        })).ValueOrThrow();

        Assert.Equal(("WEB-2", WorkItemType.Story, InProgress), (created.Key, created.Type, created.Status.Id));
        Assert.Equal((_omar.Id, Priority.High, start, due, 3m), (created.Assignee!.UserId, created.Priority, created.StartDate, created.DueDate, created.Estimate));
        Assert.Equal("WEB-1", created.Container!.Key);
        Assert.Equal(["payments", "Q4"], created.Labels!.Order());
        Assert.Contains("Check with", created.Description!.PlainText, StringComparison.Ordinal);

        var changes = await ChangesAsync("WEB-2");
        Assert.Single(changes.Select(c => c.ChangeSetId).Distinct());
        Assert.Equal(
            new[] { WorkItemField.Created, WorkItemField.Description, WorkItemField.Priority, WorkItemField.Assignee, WorkItemField.StartDate,
                WorkItemField.DueDate, WorkItemField.Estimate, WorkItemField.Container, WorkItemField.LabelAdded, WorkItemField.LabelAdded }.Order(),
            changes.Select(c => c.Field).Order());
        Assert.Equal("In Progress", changes.Single(c => c.Field == WorkItemField.Created).NewValue);
    }

    [Fact]
    public async Task JP_US1_AS6_The_assignee_and_mentioned_people_are_told_and_watch_and_the_search_finds_it()
    {
        var created = (await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue")
        {
            Description = Mentioning("Ask ", _bilal),
            AssigneeId = _omar.Id,
        })).ValueOrThrow();

        var notifications = await QueryAsync(async db => (await db.Notifications.AsNoTracking().ToListAsync(Ct))
            .ConvertAll(n => (n.RecipientId, n.Kind)));
        Assert.Equal(
            new[] { (_bilal.Id, NotificationKind.Mentioned), (_omar.Id, NotificationKind.Assigned) }.Order(),
            notifications.Order());
        var watchers = await QueryAsync(db => db.Watches.AsNoTracking().Select(w => w.UserId).ToListAsync(Ct));
        Assert.Equal(new[] { Owner.Id, _omar.Id }.Order(), watchers.Order());
        Assert.True(await QueryAsync(db => db.SearchTerms.AnyAsync(t => db.WorkItems.Any(w => w.Key == created.Key && w.Id == t.WorkItemId), Ct)));
    }

    [Fact]
    public async Task Without_choices_an_item_starts_in_the_first_to_do_column_with_medium_priority()
    {
        var created = (await TryCreateAsync(new NewWorkItem(WorkItemType.Bug, "Refund shows twice"))).ValueOrThrow();

        Assert.Equal((ToDo, Priority.Medium, (AssigneeRef?)null), (created.Status.Id, created.Priority, created.Assignee));
        Assert.Equal([WorkItemField.Created], (await ChangesAsync(created.Key)).Select(c => c.Field));
    }

    [Fact]
    public async Task JP_US1_AS3_A_sub_task_needs_a_parent_of_this_space()
    {
        var parent = (await CreateAsync(WorkItemType.Task, "Book the venue")).Key; // WEB-1

        var orphan = await TryCreateAsync(new NewWorkItem(WorkItemType.Subtask, "Call the hotel"));
        Assert.Equal(ErrorCodes.ParentRequired, orphan.Error!.Code);
        Assert.Equal("Choose the item this sub-task belongs to.", orphan.Error.Message);

        var subtask = (await TryCreateAsync(new NewWorkItem(WorkItemType.Subtask, "Call the hotel")
        {
            ParentKey = parent,
            AssigneeId = _bilal.Id,
            Priority = Priority.Low,
            Labels = ["venue"],
            DueDate = new DateOnly(2026, 10, 20),
        })).ValueOrThrow();

        Assert.Equal((WorkItemType.Subtask, parent), (subtask.Type, subtask.Parent!.Key));
        Assert.Equal((_bilal.Id, Priority.Low), (subtask.Assignee!.UserId, subtask.Priority));
        Assert.Equal(["venue"], subtask.Labels);
        Assert.Single((await ChangesAsync(subtask.Key)).Select(c => c.ChangeSetId).Distinct());
        Assert.Contains((await HistoryAsync(parent)).Items, c => c.Field == WorkItemField.SubtaskAdded && c.NewValue == subtask.Key);
    }

    [Fact]
    public async Task JP_EdgeCase_A_parent_that_is_gone_or_in_another_space_is_refused()
    {
        var parent = (await CreateAsync(WorkItemType.Task, "Book the venue")).Key;
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(parent, Ct))).IsSuccess);
        var other = await Data.ProjectAsync("PAY", Owner.Id);
        var foreign = await Data.WorkItemAsync(other.Id, "PAY", new StatusRef(other.Statuses[0].Id, "To Do", StatusCategory.ToDo), "Pay out", "a0");

        foreach (var key in new[] { parent, foreign.Key, "WEB-99" })
        {
            var refused = await TryCreateAsync(new NewWorkItem(WorkItemType.Subtask, "Call the hotel") { ParentKey = key });

            Assert.Equal(ErrorCodes.InvalidParent, refused.Error!.Code);
            Assert.Equal("The parent item no longer exists.", refused.Error.Message);
        }
    }

    [Fact]
    public async Task Only_stories_tasks_and_bugs_take_sub_tasks()
    {
        var epic = (await CreateAsync(WorkItemType.Epic, "Card refunds")).Key;

        var refused = await TryCreateAsync(new NewWorkItem(WorkItemType.Subtask, "Call the hotel") { ParentKey = epic });

        Assert.Equal(ErrorCodes.InvalidParent, refused.Error!.Code);
    }

    [Fact]
    public async Task A_refused_field_creates_nothing()
    {
        var outsider = await Data.UserAsync("zara");
        var before = await CountAsync();

        var refusals = new[]
        {
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { AssigneeId = outsider.Id }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue")
                { StartDate = new DateOnly(2026, 10, 20), DueDate = new DateOnly(2026, 10, 10) }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { Estimate = 1000 }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Epic, "Card refunds") { Estimate = 3 }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { Labels = [.. Enumerable.Range(1, 21).Select(i => $"l{i}")] }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { Labels = ["two words"] }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { StatusId = 999_999 }),
            await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue") { Description = """{"type":"doc","content":[{"type":"iframe"}]}""" }),
        };

        Assert.All(refusals, r => Assert.False(r.IsSuccess));
        Assert.Equal(ErrorCodes.NotAssignable, refusals[0].Error!.Code);
        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task JP_EdgeCase_Someone_who_became_a_viewer_is_refused()
    {
        ActAs(_omar);
        await Data.RemoveMemberAsync(await QueryAsync(db => db.Projects.Where(p => p.Key == "WEB").Select(p => p.Id).SingleAsync(Ct)), _omar.Id);
        await Data.MembersAsync("WEB", ProjectRole.Viewer, _omar);

        var refused = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Book the venue"));

        Assert.Equal(ErrorKind.Forbidden, refused.Error!.Kind);
    }
}

/// <summary>Creating into sprints (Jira parity pass US1 scenario 5). WEB is amina's Scrum space.</summary>
public sealed class CreateInScrumTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    private Task<Result<WorkItemDetails>> TryCreateAsync(NewWorkItem item) =>
        CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.CreateAsync("WEB", item, Ct));

    [Fact]
    public async Task JP_US1_AS5_A_card_created_in_a_planned_sprint_is_in_it_and_without_one_it_goes_to_the_backlog()
    {
        await StoryAsync("Earlier story");
        var sprint = await NewSprintAsync();

        var planned = (await TryCreateAsync(new NewWorkItem(WorkItemType.Story, "Refund to card") { SprintId = sprint.Id, Estimate = 5 })).ValueOrThrow();
        var unplanned = (await TryCreateAsync(new NewWorkItem(WorkItemType.Bug, "Refund shows twice"))).ValueOrThrow();

        Assert.Equal([planned.Key], KeysOf((await SprintAsync(sprint.Id)).Items));
        Assert.Equal(["WEB-1", unplanned.Key], await BacklogKeysAsync());
        Assert.Single((await QueryAsync(db => db.WorkItemChanges.AsNoTracking()
            .Where(c => db.WorkItems.Any(w => w.Key == planned.Key && w.Id == c.WorkItemId)).ToListAsync(Ct))).Select(c => c.ChangeSetId).Distinct());
        Assert.Equal(0, await QueryAsync(db => db.SprintScopeEvents.CountAsync(Ct)));
    }

    [Fact]
    public async Task A_card_created_in_the_active_sprint_is_recorded_as_added_scope()
    {
        await StoryAsync("Earlier story");
        var sprint = await NewSprintAsync();
        await MoveAsync("WEB-1", Into(sprint));
        await StartAsync(sprint.Id);

        var created = (await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Hotfix") { SprintId = sprint.Id, Estimate = 2 })).ValueOrThrow();

        Assert.Contains(created.Key, KeysOf((await SprintAsync(sprint.Id)).Items));
        var added = await QueryAsync(db => db.SprintScopeEvents.AsNoTracking()
            .Where(e => db.WorkItems.Any(w => w.Key == created.Key && w.Id == e.WorkItemId)).SingleAsync(Ct));
        Assert.Equal((SprintScopeKind.Added, 2m), (added.Kind, added.Estimate));
    }

    [Fact]
    public async Task A_completed_sprint_or_one_of_another_space_cannot_be_chosen()
    {
        await StoryAsync("Earlier story");
        var sprint = await NewSprintAsync();
        await MoveAsync("WEB-1", Into(sprint));
        await StartAsync(sprint.Id);
        await TryCompleteAsync(sprint.Id);

        var closed = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Hotfix") { SprintId = sprint.Id });
        var unknown = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Hotfix") { SprintId = 999_999 });

        Assert.False(closed.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, unknown.Error!.Kind);
    }
}

/// <summary>Stages, milestones and the stage lock from the Create form (Jira parity pass US1 scenarios 4 and 8). WEB is amina's
/// Stage-gate space with its four stages, WEB-1 to WEB-4.</summary>
public sealed class CreateInStageGateTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    private Task<Result<WorkItemDetails>> TryCreateAsync(NewWorkItem item) =>
        CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.CreateAsync("WEB", item, Ct));

    [Fact]
    public async Task JP_US1_AS4_Started_work_cannot_be_created_in_a_locked_stage()
    {
        var locked = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Plan the migration", Planning) { StatusId = InProgress });
        var waiting = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Plan the migration", Planning));
        var open = await TryCreateAsync(new NewWorkItem(WorkItemType.Task, "Kick off", Initiation) { StatusId = InProgress });

        Assert.Equal(ErrorCodes.StageLocked, locked.Error!.Code);
        Assert.Equal(ToDo, waiting.ValueOrThrow().Status.Id);
        Assert.Equal(InProgress, open.ValueOrThrow().Status.Id);
    }

    [Fact]
    public async Task JP_US1_AS8_Only_space_admins_create_stages_and_a_milestone_takes_one_date()
    {
        ActAs(Musa);
        Assert.Equal(ErrorKind.Forbidden, (await TryCreateAsync(new NewWorkItem(WorkItemType.Stage, "Handover"))).Error!.Kind);
        var milestone = (await TryCreateAsync(new NewWorkItem(WorkItemType.Milestone, "Go live", Execution) { DueDate = new DateOnly(2026, 12, 1) }))
            .ValueOrThrow();
        Assert.Equal(new DateOnly(2026, 12, 1), milestone.DueDate);

        ActAs(Owner);
        var stage = (await TryCreateAsync(new NewWorkItem(WorkItemType.Stage, "Handover"))).ValueOrThrow();
        Assert.Equal(WorkItemType.Stage, stage.Type);
        Assert.NotNull(stage.Gate);
    }

    [Fact]
    public async Task Sub_tasks_of_work_in_a_locked_stage_stay_in_to_do()
    {
        var task = await TaskInAsync(Planning, "Plan the migration");

        var refused = await TryCreateAsync(new NewWorkItem(WorkItemType.Subtask, "List the tables") { ParentKey = task, StatusId = InProgress });

        Assert.Equal(ErrorCodes.StageLocked, refused.Error!.Code);
    }
}
