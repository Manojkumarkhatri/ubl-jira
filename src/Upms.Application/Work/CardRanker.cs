using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Rank keys for positions in a column or a sub-task list (research R14). Comparisons run in SQL,
/// where the column's binary collation makes them ordinal; EF does not translate the StringComparison
/// overloads, hence the suppressed CA1309.</summary>
#pragma warning disable CA1309
internal sealed class CardRanker(IAppDbContext db, RankRebalancer rebalancer)
{
    /// <summary>A rank for the top or the bottom of a column, excluding one item (the one being moved).</summary>
    public async Task<string> AtEdgeAsync(long projectId, long columnId, long? excludeId, bool atTop, CancellationToken ct)
    {
        var column = db.WorkItems.Where(w => w.ProjectId == projectId && w.StatusId == columnId && w.ParentId == null
            && w.Id != excludeId);
        var edge = atTop
            ? await column.OrderBy(w => w.Rank).Select(w => w.Rank).FirstOrDefaultAsync(ct)
            : await column.OrderByDescending(w => w.Rank).Select(w => w.Rank).FirstOrDefaultAsync(ct);
        var rank = atTop ? Rank.Before(edge) : Rank.After(edge);
        if (rank.Length > Rank.MaxLength)
        {
            await rebalancer.RebalanceColumnAsync(projectId, columnId, ct);
            return await AtEdgeAsync(projectId, columnId, excludeId, atTop, ct);
        }

        return rank;
    }

    /// <summary>A rank just before the card <paramref name="key"/> in the column, or null if it is not there.</summary>
    public async Task<string?> BeforeCardAsync(WorkItem moving, long columnId, string key, CancellationToken ct)
    {
        var targetRank = await db.WorkItems
            .Where(w => w.ProjectId == moving.ProjectId && w.StatusId == columnId && w.ParentId == null && w.Key == key && w.Id != moving.Id)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        if (targetRank is null)
        {
            return null;
        }

        var previous = await db.WorkItems
            .Where(w => w.ProjectId == moving.ProjectId && w.StatusId == columnId && w.ParentId == null && w.Id != moving.Id
                && string.Compare(w.Rank, targetRank) < 0)
            .OrderByDescending(w => w.Rank)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        return Rank.Between(previous, targetRank);
    }

    /// <summary>A rank after the last sub-task of a parent (FR-040).</summary>
    public async Task<string> AfterLastSiblingAsync(long parentId, CancellationToken ct)
    {
        var last = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ParentId == parentId)
            .OrderByDescending(w => w.Rank)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        var rank = Rank.After(last);
        if (rank.Length > Rank.MaxLength)
        {
            await rebalancer.RebalanceSubtasksAsync(parentId, ct);
            return await AfterLastSiblingAsync(parentId, ct);
        }

        return rank;
    }

    /// <summary>The item's 1-based position among the cards of its column.</summary>
    public async Task<int> PositionOfAsync(WorkItem item, CancellationToken ct) =>
        await db.WorkItems.CountAsync(w => w.ProjectId == item.ProjectId && w.StatusId == item.StatusId && w.ParentId == null
            && w.Id != item.Id && (string.Compare(w.Rank, item.Rank) < 0 || (w.Rank == item.Rank && w.Id < item.Id)), ct) + 1;

    /// <summary>How many other cards of the item's column sort before <paramref name="rank"/>.</summary>
    public Task<int> CountBeforeAsync(WorkItem item, string rank, CancellationToken ct) =>
        db.WorkItems.CountAsync(w => w.ProjectId == item.ProjectId && w.StatusId == item.StatusId && w.ParentId == null
            && w.Id != item.Id && string.Compare(w.Rank, rank) < 0, ct);
}
#pragma warning restore CA1309
