using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Rank keys for positions in a column or a sub-task list (research R14). In Kanban and Stage-gate projects a
/// card's rank orders it within its column; in Scrum projects it orders it among all of the project's cards, so the
/// backlog, sprints and board share one order (Phase 3 research R8). Every placement asks for a key between two distinct
/// neighbours of the scope, or after the largest or before the smallest key, so it never needs a key between equal
/// keys. Comparisons run in SQL, where the column's binary collation makes them ordinal; EF does not translate the
/// StringComparison overloads, hence the suppressed CA1309.</summary>
#pragma warning disable CA1309
internal sealed class CardRanker(IAppDbContext db, RankRebalancer rebalancer)
{
    /// <summary>A rank for a new card created in a column: its bottom, or in a Scrum project after every card.</summary>
    public async Task<string> ForNewCardAsync(long projectId, ProjectTemplate template, long columnId, CancellationToken ct)
    {
        if (template != ProjectTemplate.Scrum)
        {
            return await AtEdgeAsync(projectId, template, columnId, null, null, atTop: false, ct);
        }

        var last = await Cards(projectId, null).MaxAsync(w => (string?)w.Rank, ct);
        return await ShortenedAsync(Rank.After(last), projectId, template, columnId,
            () => ForNewCardAsync(projectId, template, columnId, ct), ct);
    }

    /// <summary>A rank for the top or the bottom of a column, excluding one item (the one being moved, whose current rank
    /// is <paramref name="currentRank"/>).</summary>
    public async Task<string> AtEdgeAsync(long projectId, ProjectTemplate template, long columnId, long? excludeId, string? currentRank,
        bool atTop, CancellationToken ct)
    {
        var column = Cards(projectId, excludeId).Where(w => w.StatusId == columnId);
        var edge = atTop
            ? await column.OrderBy(w => w.Rank).Select(w => w.Rank).FirstOrDefaultAsync(ct)
            : await column.OrderByDescending(w => w.Rank).Select(w => w.Rank).FirstOrDefaultAsync(ct);
        string rank;
        if (template != ProjectTemplate.Scrum)
        {
            rank = atTop ? Rank.Before(edge) : Rank.After(edge);
        }
        else if (edge is null)
        {
            // An empty column: the card keeps its place in the project's order.
            rank = currentRank ?? Rank.After(await Cards(projectId, excludeId).MaxAsync(w => (string?)w.Rank, ct));
        }
        else
        {
            rank = atTop ? await BeforeInProjectAsync(projectId, excludeId, edge, ct) : await AfterInProjectAsync(projectId, excludeId, edge, ct);
        }

        return await ShortenedAsync(rank, projectId, template, columnId,
            () => AtEdgeAsync(projectId, template, columnId, excludeId, currentRank, atTop, ct), ct);
    }

    /// <summary>A rank just before the card <paramref name="key"/> in the column, or null if it is not there.</summary>
    public async Task<string?> BeforeCardAsync(WorkItem moving, ProjectTemplate template, long columnId, string key, CancellationToken ct)
    {
        var targetRank = await Cards(moving.ProjectId, moving.Id)
            .Where(w => w.StatusId == columnId && w.Key == key)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        if (targetRank is null)
        {
            return null;
        }

        if (template == ProjectTemplate.Scrum)
        {
            return await BeforeInProjectAsync(moving.ProjectId, moving.Id, targetRank, ct);
        }

        var previous = await Cards(moving.ProjectId, moving.Id)
            .Where(w => w.StatusId == columnId && string.Compare(w.Rank, targetRank) < 0)
            .OrderByDescending(w => w.Rank)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        return Rank.Between(previous, targetRank);
    }

    /// <summary>A rank just before <paramref name="targetRank"/> among all of a Scrum project's cards.</summary>
    public async Task<string> BeforeInProjectAsync(long projectId, long? excludeId, string targetRank, CancellationToken ct)
    {
        var previous = await Cards(projectId, excludeId)
            .Where(w => string.Compare(w.Rank, targetRank) < 0)
            .MaxAsync(w => (string?)w.Rank, ct);
        return Rank.Between(previous, targetRank);
    }

    /// <summary>A rank just after <paramref name="targetRank"/> among all of a Scrum project's cards.</summary>
    public async Task<string> AfterInProjectAsync(long projectId, long? excludeId, string targetRank, CancellationToken ct)
    {
        var next = await Cards(projectId, excludeId)
            .Where(w => string.Compare(w.Rank, targetRank) > 0)
            .MinAsync(w => (string?)w.Rank, ct);
        return Rank.Between(targetRank, next);
    }

    /// <summary>A rank after every other card of a Scrum project (a restored card, Phase 3 research R8).</summary>
    public async Task<string> AfterAllInProjectAsync(long projectId, long? excludeId, CancellationToken ct) =>
        Rank.After(await Cards(projectId, excludeId).MaxAsync(w => (string?)w.Rank, ct));

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
        await Cards(item.ProjectId, item.Id).CountAsync(w => w.StatusId == item.StatusId
            && (string.Compare(w.Rank, item.Rank) < 0 || (w.Rank == item.Rank && w.Id < item.Id)), ct) + 1;

    /// <summary>How many other cards of the item's column sort before <paramref name="rank"/>.</summary>
    public Task<int> CountBeforeAsync(WorkItem item, string rank, CancellationToken ct) =>
        Cards(item.ProjectId, item.Id).CountAsync(w => w.StatusId == item.StatusId && string.Compare(w.Rank, rank) < 0, ct);

    /// <summary>The live cards of a project (Phase 3 FR-013), without the item being placed.</summary>
    private IQueryable<WorkItem> Cards(long projectId, long? excludeId) =>
        db.WorkItems.Where(w => w.ProjectId == projectId && w.ParentId == null && w.Id != excludeId
            && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug));

    // Keys that grew too long are shortened by renumbering the scope, and the placement is asked again.
    private async Task<string> ShortenedAsync(string rank, long projectId, ProjectTemplate template, long columnId,
        Func<Task<string>> again, CancellationToken ct)
    {
        if (rank.Length <= Rank.MaxLength)
        {
            return rank;
        }

        await rebalancer.RebalanceAsync(projectId, template, columnId, ct);
        return await again();
    }
}
#pragma warning restore CA1309
