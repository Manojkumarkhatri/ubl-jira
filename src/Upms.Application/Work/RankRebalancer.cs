using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Common;

namespace Upms.Application.Work;

/// <summary>Reassigns short, evenly spaced rank keys to a column or sub-task list, keeping its order.</summary>
internal sealed class RankRebalancer(IAppDbContext db) : IRankRebalancer
{
    public async Task<int> RebalanceLongRanksAsync(CancellationToken ct)
    {
        const int threshold = Rank.RebalanceThreshold;
        var columns = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.ParentId == null && w.Rank.Length > threshold)
            .Select(w => new { w.ProjectId, w.StatusId })
            .Distinct()
            .ToListAsync(ct);
        var subtaskLists = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => w.ParentId != null && w.Rank.Length > threshold)
            .Select(w => w.ParentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        foreach (var column in columns)
        {
            await RebalanceColumnAsync(column.ProjectId, column.StatusId, ct);
        }

        foreach (var parentId in subtaskLists)
        {
            await RebalanceSubtasksAsync(parentId, ct);
        }

        return columns.Count + subtaskLists.Count;
    }

    public async Task RebalanceColumnAsync(long projectId, long statusId, CancellationToken ct)
    {
        var items = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ProjectId == projectId && w.StatusId == statusId && w.ParentId == null)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        await AssignAsync(items, ct);
    }

    public async Task RebalanceSubtasksAsync(long parentId, CancellationToken ct)
    {
        var items = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ParentId == parentId)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        await AssignAsync(items, ct);
    }

    private async Task AssignAsync(List<Domain.Work.WorkItem> items, CancellationToken ct)
    {
        var ranks = Rank.Sequence(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Rerank(ranks[i]);
        }

        await db.SaveChangesAsync(ct);
    }
}
