using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Sub-tasks (FR-017, FR-028, FR-040).</summary>
public sealed class SubtaskTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    [Fact]
    public async Task US2_AS6_Sub_tasks_get_their_own_keys_start_in_the_first_to_do_column_and_count_on_the_card()
    {
        var card = await AddAsync(InProgress, "Design the home page");

        await AddSubtaskAsync(card.Key, "Wireframes");
        await AddSubtaskAsync(card.Key, "Mock-ups");
        var parent = await AddSubtaskAsync(card.Key, "Review");

        Assert.Equal(["WEB-2", "WEB-3", "WEB-4"], parent.Subtasks.Items.Select(s => s.Key));
        Assert.All(parent.Subtasks.Items, s => Assert.Equal("To Do", s.Status.Name));

        var subtask = parent.Subtasks.Items[0];
        var afterDone = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.MarkSubtaskDoneAsync(subtask.Key, subtask.Version, Ct));

        Assert.True(afterDone.IsSuccess, afterDone.Error?.Message);
        Assert.Equal("Done", afterDone.Value!.Subtasks.Items[0].Status.Name);
        var cardNow = (await BoardAsync()).Columns[1].Cards.Single();
        Assert.Equal((1, 3), (cardNow.SubtasksDone, cardNow.SubtasksTotal));
        Assert.Contains((await HistoryAsync(card.Key)).Items, h => h.Field == WorkItemField.SubtaskAdded && h.NewValue == "WEB-2");
    }

    [Fact]
    public async Task US2_AS7_A_sub_task_opens_with_a_link_to_its_parent_and_is_never_a_board_card()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync(card.Key, "Wireframes");

        var subtask = await RequireDetailsAsync("WEB-2");

        Assert.Equal(WorkItemType.Subtask, subtask.Type);
        Assert.Equal(new ParentRef("WEB-1", "Design the home page"), subtask.Parent);
        var board = await BoardAsync();
        Assert.Equal(["WEB-1"], board.Columns.SelectMany(c => c.Cards).Select(c => c.Key));
    }

    [Fact]
    public async Task A_sub_task_cannot_have_sub_tasks()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync(card.Key, "Wireframes");

        var result = await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync("WEB-2", "Too deep", Ct));

        Assert.Equal(ErrorCodes.SubtaskDepth, result.Error!.Code);
    }

    [Fact]
    public async Task A_sub_task_status_can_be_changed_in_the_list()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        await AddSubtaskAsync(card.Key, "Wireframes");

        var saved = await EditAsync("WEB-2", new WorkItemEdit.Status(InProgress));

        Assert.Equal("In Progress", saved.Value!.Status.Name);
        Assert.Equal("In Progress", (await RequireDetailsAsync(card.Key)).Subtasks.Items.Single().Status.Name);
    }

    [Fact]
    public async Task Sub_tasks_are_listed_in_order_50_at_a_time()
    {
        var card = await AddAsync(ToDo, "Big task");
        for (var i = 1; i <= 52; i++)
        {
            await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync(card.Key, $"Step {i}", Ct));
        }

        var details = await RequireDetailsAsync(card.Key);
        var second = (await CallAsync<IWorkItemService, Result<Page<SubtaskView>>>(s =>
            s.ListSubtasksAsync(card.Key, new PageRequest(2), Ct))).ValueOrThrow();

        Assert.Equal((50, 52), (details.Subtasks.Items.Count, details.Subtasks.TotalCount));
        Assert.Equal("Step 1", details.Subtasks.Items[0].Title);
        Assert.Equal(["Step 51", "Step 52"], second.Items.Select(s => s.Title));
    }
}
