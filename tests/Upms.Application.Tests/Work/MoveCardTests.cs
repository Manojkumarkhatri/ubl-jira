using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Moving and reordering cards (FR-019, FR-020, FR-022, FR-031).</summary>
public sealed class MoveCardTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    private Task<List<WorkItemChange>> ChangesAsync(string key) => QueryAsync(db =>
        db.WorkItemChanges.AsNoTracking()
            .Where(c => db.WorkItems.Any(w => w.Id == c.WorkItemId && w.Key == key))
            .OrderBy(c => c.Id).ToListAsync(Ct));

    [Fact]
    public async Task US1_AS6_Moving_a_card_to_another_column_changes_its_status_for_everyone()
    {
        var card = await AddAsync(ToDo, "Design the home page");

        var moved = await MoveAsync(card, InProgress, CardPlacement.AtEnd);

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal(InProgress, moved.Value!.ColumnId);
        ActAs(await MemberAsync("bilal"));
        Assert.Equal(["WEB-1"], await KeysInAsync(InProgress));
        var status = (await ChangesAsync("WEB-1")).Last();
        Assert.Equal((WorkItemField.Status, "To Do", "In Progress"), (status.Field, status.OldValue, status.NewValue));
    }

    [Fact]
    public async Task US1_AS7_Dropping_a_card_above_another_keeps_the_new_order_and_records_it()
    {
        var one = await AddAsync(ToDo, "One");
        var two = await AddAsync(ToDo, "Two");
        var three = await AddAsync(ToDo, "Three");

        var moved = await MoveAsync(three, ToDo, new CardPlacement.Before(one.Key));

        Assert.True(moved.IsSuccess, moved.Error?.Message);
        ActAs(await MemberAsync("bilal"));
        Assert.Equal([three.Key, one.Key, two.Key], await KeysInAsync(ToDo));
        var rank = (await ChangesAsync(three.Key)).Last();
        Assert.Equal((WorkItemField.Rank, "3", "1"), (rank.Field, rank.OldValue, rank.NewValue));
        Assert.Equal($"moved above {one.Key}", rank.Note);
    }

    [Fact]
    public async Task Cards_can_be_placed_at_the_top_or_bottom_of_any_column()
    {
        var one = await AddAsync(ToDo, "One");
        var two = await AddAsync(ToDo, "Two");
        var three = await AddAsync(InProgress, "Three");

        await MoveAsync(two, ToDo, CardPlacement.AtTop);
        Assert.Equal([two.Key, one.Key], await KeysInAsync(ToDo));

        var refreshed = (await BoardAsync()).Columns[0].Cards.First(c => c.Key == two.Key);
        await MoveAsync(refreshed, InProgress, CardPlacement.AtTop);
        Assert.Equal([two.Key, three.Key], await KeysInAsync(InProgress));

        var oneNow = (await BoardAsync()).Columns[0].Cards.Single();
        await MoveAsync(oneNow, InProgress, CardPlacement.AtEnd);
        Assert.Equal([two.Key, three.Key, one.Key], await KeysInAsync(InProgress));
    }

    [Fact]
    public async Task US1_AS9_A_card_changed_by_someone_else_is_not_overwritten()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        ActAs(await MemberAsync("bilal"));
        await MoveAsync(card, InProgress, CardPlacement.AtEnd);

        ActAs(Owner);
        var stale = await MoveAsync(card, Done, CardPlacement.AtEnd);

        Assert.Equal(ErrorKind.Conflict, stale.Error!.Kind);
        var current = Assert.IsType<CardView>(stale.Error.Current);
        Assert.Equal(InProgress, current.ColumnId);
        Assert.Equal(["WEB-1"], await KeysInAsync(InProgress));
    }

    [Fact]
    public async Task Moving_an_unknown_card_is_not_found()
    {
        var result = await CallAsync<IBoardService, Result<CardView>>(s =>
            s.MoveCardAsync("WEB-999", ToDo, CardPlacement.AtEnd, [1, 2, 3], Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task Moving_to_a_column_of_another_project_is_not_found()
    {
        var card = await AddAsync(ToDo, "One");
        await CallAsync<Upms.Application.Projects.IProjectService, Result<string>>(s => s.CreateAsync("Mobile App", "MOB", null, Ct));
        var mobileToDo = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("MOB", false, Ct))).Value!.Columns[0].Id;

        Assert.Equal(ErrorKind.NotFound, (await MoveAsync(card, mobileToDo, CardPlacement.AtEnd)).Error!.Kind);
    }
}
