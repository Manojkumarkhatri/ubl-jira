using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Keeping rank keys short (research R14).</summary>
public sealed class RankRebalanceTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    [Fact]
    public async Task Columns_with_long_rank_keys_are_rebalanced_without_changing_the_order()
    {
        var project = await QueryAsync(db => db.Projects.AsNoTracking().SingleAsync(p => p.Key == "WEB", Ct));
        var status = new StatusRef(ToDo, "To Do", StatusCategory.ToDo);
        var low = Rank.First();
        var high = Rank.After(low);
        var ranks = new List<string>();
        for (var i = 0; i < 300; i++)
        {
            high = Rank.Between(low, high);
            ranks.Add(high);
        }

        // The 20 longest keys, inserted out of order.
        var longest = ranks.TakeLast(20).Reverse().ToList();
        Assert.All(longest, r => Assert.True(Rank.NeedsRebalance(r)));
        foreach (var rank in longest.OrderBy(_ => Guid.NewGuid()))
        {
            await Data.WorkItemAsync(project.Id, "WEB", status, $"Item {rank.Length}-{rank[^3..]}", rank);
        }

        var expectedOrder = await KeysInAsync(ToDo);

        var rebalanced = await CallAsync<IRankRebalancer, int>(r => r.RebalanceLongRanksAsync(Ct));

        Assert.Equal(1, rebalanced);
        Assert.Equal(expectedOrder, await KeysInAsync(ToDo));
        var stored = await QueryAsync(db => db.WorkItems.AsNoTracking().Select(w => w.Rank).ToListAsync(Ct));
        Assert.All(stored, r => Assert.False(Rank.NeedsRebalance(r)));
        Assert.Empty(await QueryAsync(db => db.WorkItemChanges.AsNoTracking().Where(c => c.Field == WorkItemField.Rank).ToListAsync(Ct)));
    }
}
