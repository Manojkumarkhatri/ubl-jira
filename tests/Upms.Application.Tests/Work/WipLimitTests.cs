using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Work-in-progress limits (FR-036).</summary>
public sealed class WipLimitTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    private async Task SetLimitAsync(long columnId, int? limit)
    {
        var version = (await BoardAsync()).BoardVersion;
        var result = await CallAsync<IBoardColumnService, Result<BoardColumnsView>>(s => s.SetWipLimitAsync("WEB", columnId, limit, version, Ct));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private async Task<ColumnView> ColumnAsync(long columnId) => (await BoardAsync()).Columns.Single(c => c.Id == columnId);

    [Fact]
    public async Task US3_AS4_A_column_over_its_limit_is_marked_and_moves_and_new_tasks_still_succeed()
    {
        await SetLimitAsync(InProgress, 3);
        foreach (var title in new[] { "One", "Two", "Three" })
        {
            await AddAsync(InProgress, title);
        }

        var atLimit = await ColumnAsync(InProgress);
        Assert.Equal((3, 3, false), (atLimit.CardCount, atLimit.WipLimit, atLimit.OverLimit));

        var fourth = await AddAsync(ToDo, "Four");
        var moved = await MoveAsync(fourth, InProgress, CardPlacement.AtEnd);
        Assert.True(moved.IsSuccess, moved.Error?.Message);
        var over = await ColumnAsync(InProgress);
        Assert.Equal((4, 3, true), (over.CardCount, over.WipLimit, over.OverLimit));

        await AddAsync(InProgress, "Five"); // typing into an over-limit column is never blocked
        Assert.Equal(5, (await ColumnAsync(InProgress)).CardCount);
    }

    [Fact]
    public async Task Removing_the_limit_removes_the_marker()
    {
        await SetLimitAsync(InProgress, 1);
        await AddAsync(InProgress, "One");
        await AddAsync(InProgress, "Two");
        Assert.True((await ColumnAsync(InProgress)).OverLimit);

        await SetLimitAsync(InProgress, null);

        var column = await ColumnAsync(InProgress);
        Assert.Equal(((int?)null, false), (column.WipLimit, column.OverLimit));
    }
}
