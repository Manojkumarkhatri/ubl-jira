using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

internal sealed class WorkItemStatusMover(IAppDbContext db, IProjectWorkflow workflow, ICurrentUser currentUser, TimeProvider time)
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
        var statuses = await workflow.StatusesAsync(projectId, ct);
        var from = statuses.Single(s => s.Id == fromStatusId);
        var to = statuses.SingleOrDefault(s => s.Id == toStatusId)
            ?? throw new InvalidOperationException("The destination column is not on the same board.");
        var context = ChangeContext.New(currentUser.UserId ?? throw new InvalidOperationException("No signed-in user."),
            time.GetUtcNow());

        // Cards keep their order and follow the destination's last card; sub-tasks keep their place under their parent.
        var last = await db.WorkItems.IgnoreQueryFilters()
            .Where(w => w.ProjectId == projectId && w.StatusId == toStatusId && w.ParentId == null)
            .OrderByDescending(w => w.Rank)
            .Select(w => w.Rank)
            .FirstOrDefaultAsync(ct);
        foreach (var item in items)
        {
            var rank = item.Rank;
            if (item.ParentId is null)
            {
                last = Rank.After(last);
                rank = last;
            }

            item.Move(ToRef(from), ToRef(to), rank, null, null, note, context);
        }
    }

    private static StatusRef ToRef(StatusInfo status) => new(status.Id, status.Name, status.Category);
}
