using Upms.Application.Tests.Fixtures;

namespace Upms.Application.Tests.Work;

/// <summary>A Scrum board without an active sprint still has its columns, so that they can be managed there (Jira parity
/// pass FR-013). WEB is amina's Scrum space.</summary>
public sealed class ScrumBoardColumnsTests(SqlServerFixture fixture) : ScrumTestBase(fixture)
{
    [Fact]
    public async Task JP_US2_FR013_A_Scrum_board_without_a_sprint_shows_its_columns_empty()
    {
        await StoryAsync("Refund to card");

        var board = await BoardAsync();

        Assert.Null(board.Sprint);
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], board.Columns.Select(c => c.Name));
        Assert.All(board.Columns, c => Assert.Empty(c.Cards));
        Assert.True(board.CanManageColumns);
    }
}
