using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>What creating, deleting and restoring a stage means for its gate (Phase 3 data-model.md "Gate rules"): every
/// new stage gets a gate, an approved stage cannot be deleted, deleting a stage cancels a pending request, and a
/// restored stage starts over with its gate not requested.</summary>
internal sealed class StageGateRules(IAppDbContext db)
{
    /// <summary>A new, not requested gate for a stage that has been saved (it needs the stage's ID).</summary>
    public void AddGateFor(WorkItem stage) => db.StageGates.Add(StageGate.Create(stage.ProjectId, stage.Id));

    public async Task<AppError?> CheckDeleteAsync(WorkItem stage, CancellationToken ct) =>
        await db.StageGates.AnyAsync(g => g.StageId == stage.Id && g.State == GateState.Approved, ct)
            ? AppError.Rule(ErrorCodes.GateApproved, $"{stage.Title}'s gate is approved, so the stage keeps its place and cannot be deleted.")
            : null;

    /// <summary>Cancels the pending request of a deleted stage, or starts a restored stage's gate over.</summary>
    public async Task ResetAsync(WorkItem stage, ChangeContext context, CancellationToken ct)
    {
        var gate = await db.StageGates.Include(g => g.Requests).SingleOrDefaultAsync(g => g.StageId == stage.Id, ct);
        if (gate is null)
        {
            AddGateFor(stage);
            return;
        }

        gate.Cancel(context);
    }

    /// <summary>A stage's gate as its drawer shows it.</summary>
    public async Task<GateSummary?> SummaryAsync(long stageId, CancellationToken ct)
    {
        var gate = await db.StageGates.AsNoTracking()
            .Include(g => g.Approvers)
            .Include(g => g.Requests).ThenInclude(r => r.Decisions)
            .SingleOrDefaultAsync(g => g.StageId == stageId, ct);
        if (gate is null)
        {
            return null;
        }

        var approvals = gate.CurrentRequest?.Decisions.Count(d => d.Decision == GateDecisionKind.Approve)
            ?? (gate.IsApproved ? gate.ApprovalsNeeded : 0);
        return new GateSummary(gate.State, approvals, gate.ApprovalsNeeded);
    }
}
