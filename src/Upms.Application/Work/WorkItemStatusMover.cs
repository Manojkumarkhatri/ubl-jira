using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

internal sealed class WorkItemStatusMover(IAppDbContext db, IProjectWorkflow workflow, ICurrentUser currentUser, StageLocks locks,
    StatusNotices notices,
    TimeProvider time)
    : IWorkItemStatusMover
{
    public async Task<IReadOnlyDictionary<long, int>> CountInStatusesAsync(IReadOnlyCollection<long> statusIds, CancellationToken ct)
    {
        if (statusIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var ids = statusIds.ToList();
        return await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(w => ids.Contains(w.StatusId))
            .GroupBy(w => w.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StatusId, x => x.Count, ct);
    }

    public async Task MoveAllAsync(long fromStatusId, long toStatusId, string note, CancellationToken ct)
    {
        var items = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.StatusId == fromStatusId)
            .OrderBy(w => w.Rank).ThenBy(w => w.Id)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            return;
        }

        var projectId = items[0].ProjectId;
        var board = await workflow.GetBoardInfoAsync(projectId, ct);
        var statuses = board.Statuses;
        var from = statuses.Single(s => s.Id == fromStatusId);
        var to = statuses.SingleOrDefault(s => s.Id == toStatusId)
            ?? throw new InvalidOperationException("The destination column is not on the same board.");
        var context = ChangeContext.New(currentUser.UserId ?? throw new InvalidOperationException("No signed-in user."),
            time.GetUtcNow());

        // Cards keep their order and follow the destination's last card; sub-tasks keep their place under their parent, and
        // epics, stages and milestones, which have no card order, keep their rank. In a Scrum project a card's rank already
        // orders it among all the project's cards, so every item keeps its rank (Phase 3 research R8).
        var last = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ProjectId == projectId && w.StatusId == toStatusId && w.ParentId == null
                && (w.Type == WorkItemType.Task || w.Type == WorkItemType.Story || w.Type == WorkItemType.Bug))
            .OrderByDescending(w => w.Rank)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        foreach (var item in items)
        {
            var rank = item.Rank;
            if (board.Template != ProjectTemplate.Scrum && item.ParentId is null && WorkItemTypes.IsCard(item.Type))
            {
                last = Rank.After(last);
                rank = last;
            }

            item.Move(ToRef(from), ToRef(to), rank, null, null, note, context);
        }

        // Watchers of live items hear about the move; the caller saves it with them (Phase 4 FR-015).
        await notices.RaiseAsync(items.Where(i => !i.IsDeleted).ToList(), to.Name, context.ActorId, ct);
    }

    public Task<int> CountInLockedStagesAsync(long statusId, CancellationToken ct) => locks.CountLockedInStatusAsync(statusId, ct);

    private static StatusRef ToRef(StatusInfo status) => new(status.Id, status.Name, status.Category);
}
