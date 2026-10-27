using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Moving a card into another lane changes its epic, stage or assignee with the column, in one change (Jira parity
/// pass US5 scenario 5, FR-026; research R17).</summary>
public sealed class LaneMoveTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private async Task<CardView> CardAsync(string key) =>
        (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == key);

    private Task<Result<CardView>> MoveAsync(CardView card, long columnId, LaneChange lane) =>
        CallAsync<IBoardService, Result<CardView>>(s => s.MoveCardAsync(card.Key, columnId, CardPlacement.AtEnd, card.Version, lane, Ct));

    private Task<List<WorkItemChange>> ChangesAsync(string key) => QueryAsync(db =>
        db.WorkItemChanges.AsNoTracking()
            .Where(c => db.WorkItems.Any(w => w.Id == c.WorkItemId && w.Key == key))
            .OrderBy(c => c.Id).ToListAsync(Ct));

    /// <summary>Epics WEB-1 and WEB-2, and story WEB-3 in WEB-1.</summary>
    private async Task<CardView> StoryInEpicAsync()
    {
        await CreateAsync(WorkItemType.Epic, "Card refunds");
        await CreateAsync(WorkItemType.Epic, "Wallet refunds");
        await CreateAsync(WorkItemType.Story, "Refund to card", "WEB-1");
        return await CardAsync("WEB-3");
    }

    [Fact]
    public async Task JP_US5_AS5_A_card_dropped_in_another_epics_lane_changes_its_epic_and_column_in_one_change()
    {
        var card = await StoryInEpicAsync();
        var before = (await ChangesAsync(card.Key)).Count;

        var moved = await MoveAsync(card, InProgress, new LaneChange.Container("WEB-2"));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal(("WEB-2", InProgress), (moved.Value!.Container?.Key, moved.Value.ColumnId));
        var changes = (await ChangesAsync(card.Key)).Skip(before).ToList();
        Assert.Equal([WorkItemField.Container, WorkItemField.Status], changes.Select(c => c.Field));
        Assert.Equal(("WEB-1 Card refunds", "WEB-2 Wallet refunds"), (changes[0].OldValue, changes[0].NewValue));
        Assert.Single(changes.Select(c => c.ChangeSetId).Distinct());
    }

    [Fact]
    public async Task The_no_epic_lane_takes_the_card_out_of_its_epic()
    {
        var card = await StoryInEpicAsync();

        var moved = await MoveAsync(card, ToDo, new LaneChange.Container(null));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Null(moved.Value!.Container);
        Assert.Null((await RequireDetailsAsync(card.Key)).Container);
    }

    [Fact]
    public async Task JP_US5_AS5_A_card_dropped_in_a_persons_lane_is_assigned_to_them_and_they_are_told()
    {
        var bilal = await MemberAsync("bilal");
        var card = await AddAsync(ToDo, "Refund to card");

        var moved = await MoveAsync(card, InProgress, new LaneChange.Assignee(bilal.Id));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal((bilal.Id, InProgress), (moved.Value!.Assignee?.UserId, moved.Value.ColumnId));
        Assert.Contains(await QueryAsync(db => db.Notifications.AsNoTracking().Select(n => new { n.RecipientId, n.Kind }).ToListAsync(Ct)),
            n => n.RecipientId == bilal.Id && n.Kind == NotificationKind.Assigned);
        Assert.Contains(bilal.Id, await QueryAsync(db => db.Watches.AsNoTracking().Select(w => w.UserId).ToListAsync(Ct)));
        var changes = await ChangesAsync(card.Key);
        Assert.Equal(changes[^2].ChangeSetId, changes[^1].ChangeSetId);
        Assert.Equal((WorkItemField.Assignee, WorkItemField.Status), (changes[^2].Field, changes[^1].Field));
    }

    [Fact]
    public async Task The_unassigned_lane_clears_the_assignee()
    {
        var bilal = await MemberAsync("bilal");
        var card = await AddAsync(ToDo, "Refund to card");
        (await EditAsync(card.Key, new WorkItemEdit.Assignee(bilal.Id))).ValueOrThrow();

        var moved = await MoveAsync(await CardAsync(card.Key), ToDo, new LaneChange.Assignee(null));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Null(moved.Value!.Assignee);
    }

    [Fact]
    public async Task A_sub_task_follows_its_parents_epic()
    {
        await StoryInEpicAsync();
        await AddSubtaskAsync("WEB-3", "Write the API"); // WEB-4
        var subtask = await CardAsync("WEB-4");

        var moved = await MoveAsync(subtask, InProgress, new LaneChange.Container("WEB-2"));

        Assert.Equal(ErrorCodes.SubtaskFollowsParent, moved.Error!.Code);
        Assert.Equal("A sub-task follows its parent's epic.", moved.Error.Message);
        Assert.Equal(ToDo, (await CardAsync("WEB-4")).ColumnId);
    }

    [Fact]
    public async Task A_sub_task_can_change_person_lane()
    {
        var bilal = await MemberAsync("bilal");
        var card = await AddAsync(ToDo, "Refund to card");
        await AddSubtaskAsync(card.Key, "Write the API"); // WEB-2

        var moved = await MoveAsync(await CardAsync("WEB-2"), ToDo, new LaneChange.Assignee(bilal.Id));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal(bilal.Id, moved.Value!.Assignee?.UserId);
    }

    [Fact]
    public async Task A_refused_lane_change_changes_nothing()
    {
        var vera = await MemberAsync("vera", ProjectRole.Viewer);
        var card = await AddAsync(ToDo, "Refund to card");
        var before = (await ChangesAsync(card.Key)).Count;

        var moved = await MoveAsync(card, InProgress, new LaneChange.Assignee(vera.Id));

        Assert.Equal(ErrorCodes.NotAssignable, moved.Error!.Code);
        var after = await CardAsync(card.Key);
        Assert.Equal((ToDo, (Guid?)null), (after.ColumnId, after.Assignee?.UserId));
        Assert.Equal(before, (await ChangesAsync(card.Key)).Count);
    }

    [Fact]
    public async Task A_deactivated_person_cannot_be_given_the_card()
    {
        var bilal = await Data.UserAsync("bilal", isActive: false);
        await Data.MembersAsync("WEB", ProjectRole.Member, bilal);
        var card = await AddAsync(ToDo, "Refund to card");

        var moved = await MoveAsync(card, ToDo, new LaneChange.Assignee(bilal.Id));

        Assert.Equal(ErrorCodes.NotAssignable, moved.Error!.Code);
    }

    [Fact]
    public async Task A_stale_card_is_a_conflict()
    {
        var card = await StoryInEpicAsync();
        (await EditAsync(card.Key, new WorkItemEdit.Title("Refund to a card"))).ValueOrThrow();

        var moved = await MoveAsync(card, InProgress, new LaneChange.Container("WEB-2"));

        Assert.Equal(ErrorKind.Conflict, moved.Error!.Kind);
        Assert.Equal("WEB-1", (await CardAsync(card.Key)).Container?.Key);
    }

    [Fact]
    public async Task Viewers_cannot_move_cards_between_lanes()
    {
        var card = await StoryInEpicAsync();
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));

        var moved = await MoveAsync(card, ToDo, new LaneChange.Container("WEB-2"));

        Assert.Equal(ErrorKind.Forbidden, moved.Error!.Kind);
    }
}

/// <summary>Started work cannot join a locked stage through its lane either (Phase 3 FR-033; research R17).</summary>
public sealed class StageLaneMoveTests(SqlServerFixture fixture) : StageGateTestBase(fixture)
{
    [Fact]
    public async Task Started_work_cannot_be_dropped_in_a_locked_stages_lane()
    {
        var task = await TaskInAsync(Initiation, "Write the charter");
        (await StartAsync(task)).ValueOrThrow();
        var card = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == task);

        var moved = await CallAsync<IBoardService, Result<CardView>>(s =>
            s.MoveCardAsync(task, InProgress, CardPlacement.AtEnd, card.Version, new LaneChange.Container(Planning), Ct));

        Assert.Equal(ErrorCodes.StageLocked, moved.Error!.Code);
        Assert.Equal(Initiation, (await RequireDetailsAsync(task)).Container?.Key);
    }

    [Fact]
    public async Task Work_still_to_do_can_join_a_locked_stage()
    {
        var task = await TaskInAsync(Initiation, "Write the charter");
        var card = (await BoardAsync()).Columns.SelectMany(c => c.Cards).Single(c => c.Key == task);

        var moved = await CallAsync<IBoardService, Result<CardView>>(s =>
            s.MoveCardAsync(task, ToDo, CardPlacement.AtEnd, card.Version, new LaneChange.Container(Planning), Ct));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal(Planning, moved.Value!.Container?.Key);
    }
}
