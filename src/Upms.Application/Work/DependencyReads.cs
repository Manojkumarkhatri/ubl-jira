using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>An item's dependencies with their late marks, computed on read (Phase 3 FR-049, FR-051; research R19), and the
/// removal of a deleted item's links (FR-052).</summary>
internal sealed class DependencyReads(IAppDbContext db, IProjectWorkflow workflow)
{
    /// <param name="statuses">The project's columns when the caller has read them already, as the drawer has.</param>
    public async Task<DependencyList> ListAsync(WorkItem item, IReadOnlyList<StatusInfo>? statuses, CancellationToken ct)
    {
        // Both directions in one read: every drawer and every save that returns one asks for them (SC-002).
        var waitsFor =
            from d in db.Dependencies.AsNoTracking()
            join w in db.WorkItems.AsNoTracking() on d.PredecessorId equals w.Id
            where d.SuccessorId == item.Id
            select new { WaitsFor = d.SuccessorId == item.Id, w.Number, w.Key, w.Type, w.Title, w.StatusId, w.StartDate, w.DueDate, w.ResolvedAt };
        var blocks =
            from d in db.Dependencies.AsNoTracking()
            join w in db.WorkItems.AsNoTracking() on d.SuccessorId equals w.Id
            where d.PredecessorId == item.Id
            select new { WaitsFor = d.SuccessorId == item.Id, w.Number, w.Key, w.Type, w.Title, w.StatusId, w.StartDate, w.DueDate, w.ResolvedAt };
        var links = await waitsFor.Concat(blocks).ToListAsync(ct);
        if (links.Count == 0)
        {
            return DependencyList.Empty;
        }

        statuses ??= await workflow.StatusesAsync(item.ProjectId, ct);
        return new DependencyList(
            links.Where(w => w.WaitsFor).OrderBy(w => w.Number).Select(w => new DependencyView(w.Key, w.Type, w.Title,
                WorkItemReads.ToOption(statuses, w.StatusId), w.StartDate, w.DueDate,
                DependencyGraph.IsLate(w.ResolvedAt is null, w.DueDate, item.StartDate, item.DueDate))).ToList(),
            links.Where(w => !w.WaitsFor).OrderBy(w => w.Number).Select(w => new DependencyView(w.Key, w.Type, w.Title,
                WorkItemReads.ToOption(statuses, w.StatusId), w.StartDate, w.DueDate,
                DependencyGraph.IsLate(item.ResolvedAt is null, item.DueDate, w.StartDate, w.DueDate))).ToList());
    }

    /// <summary>Deletes the links of deleted (or moved) items and records the removal on each linked item that stays (FR-052).</summary>
    public async Task RemoveLinksAsync(IReadOnlyCollection<WorkItem> deleted, ChangeContext context, CancellationToken ct,
        string note = "deleted")
    {
        var ids = deleted.Select(d => d.Id).ToList();
        var links = await db.Dependencies.Where(d => ids.Contains(d.PredecessorId) || ids.Contains(d.SuccessorId)).ToListAsync(ct);
        if (links.Count == 0)
        {
            return;
        }

        var others = links.SelectMany(l => new[] { l.PredecessorId, l.SuccessorId }).Where(id => !ids.Contains(id)).Distinct().ToList();
        var linked = await db.WorkItems.Where(w => others.Contains(w.Id)).ToDictionaryAsync(w => w.Id, ct);
        var byId = deleted.ToDictionary(d => d.Id);
        foreach (var link in links)
        {
            if (linked.TryGetValue(link.SuccessorId, out var waiting) && byId.TryGetValue(link.PredecessorId, out var waitedFor))
            {
                waiting.RecordActivity(WorkItemField.DependencyRemoved, WaitsForText(waitedFor), null, context, note);
            }
            else if (linked.TryGetValue(link.PredecessorId, out var blocking) && byId.TryGetValue(link.SuccessorId, out var gone))
            {
                blocking.RecordActivity(WorkItemField.DependencyRemoved, WaitedForByText(gone), null, context, note);
            }
        }

        db.Dependencies.RemoveRange(links);
    }

    public static string WaitsForText(WorkItem waitedFor) => $"waits for {waitedFor.Key} {waitedFor.Title}";

    public static string WaitedForByText(WorkItem waiting) => $"is waited for by {waiting.Key} {waiting.Title}";
}
