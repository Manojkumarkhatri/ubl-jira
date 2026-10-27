using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Deleting and restoring tasks (FR-024, FR-033).</summary>
public sealed class DeleteRestoreTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private Task<Result> DeleteAsync(string key) => CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(key, Ct));

    [Fact]
    public async Task US2_AS11_The_creator_deletes_a_task_with_its_sub_tasks_after_a_preview()
    {
        var bilal = await MemberAsync("bilal");
        ActAs(bilal);
        var card = await AddAsync(ToDo, "Bilal's task");
        await AddSubtaskAsync(card.Key, "One");
        await AddSubtaskAsync(card.Key, "Two");

        var preview = (await CallAsync<IWorkItemService, Result<DeletePreview>>(s => s.PreviewDeleteAsync(card.Key, Ct))).ValueOrThrow();
        Assert.Equal((card.Key, 2), (preview.Key, preview.SubtaskCount));

        Assert.True((await DeleteAsync(card.Key)).IsSuccess);

        Assert.Empty(await KeysInAsync(ToDo));
        foreach (var key in new[] { "WEB-1", "WEB-2", "WEB-3" })
        {
            Assert.Equal(ErrorKind.NotFound, (await DetailsAsync(key)).Error!.Kind);
        }
    }

    [Fact]
    public async Task US2_AS11_Project_Admins_and_administrators_may_delete_but_other_members_may_not()
    {
        var card = await AddAsync(ToDo, "Amina's task");
        var second = await AddAsync(ToDo, "Another");

        ActAs(await MemberAsync("bilal"));
        Assert.Equal(ErrorKind.Forbidden, (await DeleteAsync(card.Key)).Error!.Kind);
        Assert.False((await RequireDetailsAsync(card.Key)).CanDelete);

        ActAs(Owner);
        Assert.True((await DeleteAsync(card.Key)).IsSuccess);

        ActAs(await Data.AdministratorAsync());
        Assert.True((await DeleteAsync(second.Key)).IsSuccess);
    }

    [Fact]
    public async Task US2_AS11_Administrators_list_deleted_tasks_and_restore_them_with_their_sub_tasks()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync(card.Key, "Wireframes");
        await DeleteAsync(card.Key);

        // Anyone who sees the space reads its archived items, as in Jira; only administrators restore them.
        ActAs(Owner);
        Assert.Single((await CallAsync<IWorkItemService, Result<Page<DeletedItemView>>>(s =>
            s.ListDeletedAsync("WEB", PageRequest.First, Ct))).ValueOrThrow().Items);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(card.Key, Ct))).Error!.Kind);

        ActAs(await Data.AdministratorAsync());
        var deleted = (await CallAsync<IWorkItemService, Result<Page<DeletedItemView>>>(s =>
            s.ListDeletedAsync("WEB", PageRequest.First, Ct))).ValueOrThrow();
        var item = Assert.Single(deleted.Items);
        Assert.Equal((card.Key, 1, Owner.DisplayName), (item.Key, item.SubtaskCount, item.DeletedByName));

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(card.Key, Ct))).IsSuccess);

        Assert.Equal([card.Key, "WEB-2"], await KeysInAsync(ToDo)); // the card and its restored sub-task (Jira parity pass FR-027a)
        Assert.Single((await RequireDetailsAsync(card.Key)).Subtasks.Items);
        var fields = (await HistoryAsync(card.Key)).Items.Select(h => h.Field).ToList();
        Assert.Contains(WorkItemField.Deleted, fields);
        Assert.Contains(WorkItemField.Restored, fields);
    }

    [Fact]
    public async Task Keys_are_never_reused_after_a_deletion()
    {
        var first = await AddAsync(ToDo, "First");
        await DeleteAsync(first.Key);

        var next = await AddAsync(ToDo, "Second");

        Assert.Equal("WEB-2", next.Key);
    }

    [Fact]
    public async Task Deleted_tasks_cannot_be_edited_or_moved()
    {
        var card = await AddAsync(ToDo, "Doomed");
        var version = (await RequireDetailsAsync(card.Key)).Version;
        await DeleteAsync(card.Key);

        Assert.Equal(ErrorKind.NotFound, (await EditAsync(card.Key, new WorkItemEdit.Title("Revived"), version)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await MoveAsync(card, Done, CardPlacement.AtEnd)).Error!.Kind);
    }
}

/// <summary>Deleting and restoring cards of a Scrum project's sprints (Phase 3 data-model.md "WorkItem"; research R10,
/// R11).</summary>
public sealed class ScrumDeleteRestoreTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    [Fact]
    public async Task A_card_deleted_from_the_active_sprint_is_removed_and_restored_to_the_backlog()
    {
        var story = await StoryAsync("Refund to card", 3);
        var sprint = await NewSprintAsync();
        await MoveAsync(story, Into(sprint));
        await StartAsync(sprint.Id);

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(story, Ct))).IsSuccess);

        Assert.Null(await SprintIdOfAsync(story));
        Assert.Equal([(story, SprintScopeKind.Committed, (decimal?)3m), (story, SprintScopeKind.Removed, 3m)], await ScopeEventsAsync(sprint.Id));

        ActAs(await Data.AdministratorAsync());
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(story, Ct))).IsSuccess);
        ActAs(Owner);
        Assert.Equal([story], await BacklogKeysAsync());
        Assert.Empty((await SprintAsync(sprint.Id)).Items);
    }

    [Fact]
    public async Task A_card_deleted_from_a_planned_sprint_leaves_it_without_a_scope_event()
    {
        var story = await StoryAsync("Refund to card");
        var sprint = await NewSprintAsync();
        await MoveAsync(story, Into(sprint));

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync(story, Ct))).IsSuccess);

        Assert.Null(await SprintIdOfAsync(story));
        Assert.Empty(await ScopeEventsAsync(sprint.Id));
        var sprintChanges = await QueryAsync(db => db.WorkItemChanges.AsNoTracking()
            .Where(c => c.Field == WorkItemField.Sprint && db.WorkItems.IgnoreQueryFilters().Any(w => w.Id == c.WorkItemId && w.Key == story))
            .OrderBy(c => c.Id)
            .Select(c => new { c.OldValue, c.NewValue })
            .ToListAsync(Ct));
        Assert.Equal([(null, "WEB Sprint 1"), ("WEB Sprint 1", null)], sprintChanges.Select(c => (c.OldValue, c.NewValue)));
    }
}
