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

        ActAs(Owner);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IWorkItemService, Result<Page<DeletedItemView>>>(s =>
            s.ListDeletedAsync("WEB", PageRequest.First, Ct))).Error!.Kind);

        ActAs(await Data.AdministratorAsync());
        var deleted = (await CallAsync<IWorkItemService, Result<Page<DeletedItemView>>>(s =>
            s.ListDeletedAsync("WEB", PageRequest.First, Ct))).ValueOrThrow();
        var item = Assert.Single(deleted.Items);
        Assert.Equal((card.Key, 1, Owner.DisplayName), (item.Key, item.SubtaskCount, item.DeletedByName));

        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.RestoreAsync(card.Key, Ct))).IsSuccess);

        Assert.Equal([card.Key], await KeysInAsync(ToDo));
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
