using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Reading the board (FR-017, FR-020, FR-021).</summary>
public sealed class BoardQueryTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    [Fact]
    public async Task Columns_come_in_position_order_with_cards_in_rank_order_and_counts()
    {
        await AddAsync(ToDo, "First");
        await AddAsync(ToDo, "Second");
        await AddAsync(InProgress, "Third");

        var board = await BoardAsync();

        Assert.Equal(["To Do", "In Progress", "Done"], board.Columns.Select(c => c.Name));
        Assert.Equal(["First", "Second"], board.Columns[0].Cards.Select(c => c.Title));
        Assert.Equal([2, 1, 0], board.Columns.Select(c => c.CardCount));
        Assert.All(board.Columns[0].Cards, c => Assert.Equal(Upms.Domain.Work.Priority.Medium, c.Priority));
        Assert.True(board.CanManageColumns);
    }

    [Fact]
    public async Task US1_AS10_Done_columns_show_tasks_completed_in_the_last_14_days_unless_all_are_requested()
    {
        var old = await AddAsync(ToDo, "Finished long ago");
        await MoveAsync(old, Done, CardPlacement.AtEnd);
        Harness.Time.Advance(TimeSpan.FromDays(20));
        var recent = await AddAsync(ToDo, "Finished yesterday");
        await MoveAsync(recent, Done, CardPlacement.AtEnd);

        var board = await BoardAsync();
        var doneColumn = board.Columns[2];
        Assert.Equal(["Finished yesterday"], doneColumn.Cards.Select(c => c.Title));
        Assert.Equal(1, board.HiddenDoneCount);

        var all = await BoardAsync(showAllDone: true);
        Assert.Equal(["Finished long ago", "Finished yesterday"], all.Columns[2].Cards.Select(c => c.Title));
        Assert.Equal(0, all.HiddenDoneCount);
    }

    [Fact]
    public async Task Every_user_sees_the_same_board()
    {
        await AddAsync(ToDo, "One");
        await AddAsync(ToDo, "Two");
        var mine = await BoardAsync();

        ActAs(await MemberAsync("bilal"));
        var theirs = await BoardAsync();

        Assert.Equal(mine.Columns.SelectMany(c => c.Cards.Select(x => x.Key)), theirs.Columns.SelectMany(c => c.Cards.Select(x => x.Key)));
        Assert.False(theirs.CanManageColumns);
    }

    [Fact]
    public async Task Unknown_projects_are_not_found()
    {
        var result = await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("NOPE", false, Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }
}
