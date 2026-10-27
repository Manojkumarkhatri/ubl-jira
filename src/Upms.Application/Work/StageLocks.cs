using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The stage lock, one check for every path that changes a status (Phase 3 FR-032–FR-034; research R15): work in
/// a locked stage (the item's stage, its parent's stage for a sub-task, or the stage itself) cannot leave a "to do"
/// column, started work cannot join a locked stage, and a stage is completed only after its gate is approved. It applies
/// to everyone, administrators included.</summary>
internal sealed class StageLocks(IAppDbContext db)
{
    /// <summary>The project's live stages in order, with whether each gate is approved.</summary>
    public async Task<StageOrder> OrderAsync(long projectId, CancellationToken ct)
    {
        var stages = await (
                from w in db.WorkItems.AsNoTracking()
                where w.ProjectId == projectId && w.Type == WorkItemType.Stage
                select new
                {
                    w.Id,
                    w.Key,
                    w.Title,
                    w.Rank,
                    Approved = db.StageGates.Any(g => g.StageId == w.Id && g.State == GateState.Approved),
                })
            .ToListAsync(ct);
        return new StageOrder(stages.Select(s => new StageSlot(s.Id, s.Key, s.Title, s.Rank, s.Approved)));
    }

    /// <summary>The stage whose gate must be approved before the item's work can start, or null when it is not in a
    /// locked stage.</summary>
    public async Task<StageSlot?> BlockingGateAsync(WorkItem item, CancellationToken ct)
    {
        var stageId = item.Type == WorkItemType.Stage
            ? item.Id
            : item.ParentId is { } parentId
                ? await db.WorkItems.AsNoTracking().Where(p => p.Id == parentId).Select(p => p.ContainerId).FirstOrDefaultAsync(ct)
                : item.ContainerId;
        if (stageId is not { } id || !await db.WorkItems.AsNoTracking().AnyAsync(w => w.Id == id && w.Type == WorkItemType.Stage, ct))
        {
            return null; // in no stage (or in an epic): never locked
        }

        return (await OrderAsync(item.ProjectId, ct)).BlockingGateFor(id);
    }

    /// <summary>Refuses moving locked work out of "to do" (<c>StageLocked</c>, naming the gate) and completing a stage
    /// whose gate is not approved (<c>GateNotApproved</c>).</summary>
    public async Task<AppError?> CheckMoveAsync(WorkItem item, StatusInfo from, StatusInfo to, CancellationToken ct)
    {
        if (item.Type == WorkItemType.Stage && to.Category == StatusCategory.Done && from.Category != StatusCategory.Done
            && !await db.StageGates.AnyAsync(g => g.StageId == item.Id && g.State == GateState.Approved, ct))
        {
            return AppError.Rule(ErrorCodes.GateNotApproved, $"{item.Title} can be completed once its gate is approved.");
        }

        if (from.Category == StatusCategory.ToDo && to.Category != StatusCategory.ToDo && await BlockingGateAsync(item, ct) is { } gate)
        {
            return Locked(gate);
        }

        return null;
    }

    /// <summary>Refuses putting started work into a locked stage (FR-033).</summary>
    public async Task<AppError?> CheckPlaceInAsync(WorkItem item, StatusCategory category, long targetId, CancellationToken ct)
    {
        if (category == StatusCategory.ToDo || !await db.WorkItems.AsNoTracking().AnyAsync(w => w.Id == targetId && w.Type == WorkItemType.Stage, ct))
        {
            return null;
        }

        return (await OrderAsync(item.ProjectId, ct)).BlockingGateFor(targetId) is { } gate
            ? AppError.Rule(ErrorCodes.StageLocked, $"Started work cannot join a locked stage. The {gate.Title} gate must be approved first.")
            : null;
    }

    /// <summary>How many live items in the status belong to a locked stage (FR-033: deleting their column may send them
    /// only to a "to do" column).</summary>
    public async Task<int> CountLockedInStatusAsync(long statusId, CancellationToken ct)
    {
        var items = await db.WorkItems.AsNoTracking()
            .Where(w => w.StatusId == statusId)
            .Select(w => new
            {
                w.ProjectId,
                StageId = w.Type == WorkItemType.Stage
                    ? (long?)w.Id
                    : w.ParentId != null ? db.WorkItems.Where(p => p.Id == w.ParentId).Select(p => p.ContainerId).FirstOrDefault() : w.ContainerId,
            })
            .Where(w => w.StageId != null)
            .ToListAsync(ct);
        if (items.Count == 0)
        {
            return 0;
        }

        var order = await OrderAsync(items[0].ProjectId, ct);
        return items.Count(i => order.IsLocked(i.StageId!.Value));
    }

    public static AppError Locked(StageSlot gate) =>
        AppError.Rule(ErrorCodes.StageLocked, $"This work cannot start until the {gate.Title} gate is approved.");
}
