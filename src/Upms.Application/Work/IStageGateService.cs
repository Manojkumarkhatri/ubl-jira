using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The stages and gates of Stage-gate projects (Phase 3 FR-031–FR-040). Other projects have none:
/// <c>NotFound</c>. Stage and gate changes take the project's planning lock (research R9).</summary>
public interface IStageGateService
{
    /// <summary>The stages in order, each with its place and its gate. View right.</summary>
    Task<Result<StagesView>> GetAsync(string projectKey, CancellationToken ct);

    /// <summary>A new stage with its gate, after <paramref name="afterStageKey"/> (the current stage or a locked one) or
    /// at the end. Manage right; <c>StageOrderFixed</c>.</summary>
    Task<Result<StagesView>> AddStageAsync(string projectKey, string title, string? afterStageKey, CancellationToken ct);

    /// <summary>Moves a locked stage up (-1) or down (+1) among the locked stages. Manage right; <c>StageOrderFixed</c>.</summary>
    Task<Result<StagesView>> MoveStageAsync(string stageKey, int offset, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Active accounts across the organization, at most 20, for naming approvers. Manage right.</summary>
    Task<Result<IReadOnlyList<ApproverOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct);

    /// <summary>Names the gate's approvers, adding non-members as Viewers, and sets the approvals needed (null for all)
    /// and the requester exclusion. Manage right; <c>GateRequestPending</c>, <c>GateApproved</c>, validation.</summary>
    Task<Result<StageView>> SetUpGateAsync(string stageKey, IReadOnlyList<Guid> approverIds, int? requiredApprovals,
        bool excludeRequester, byte[] expectedGateVersion, CancellationToken ct);

    /// <summary>The stage's open items, shown before approval is requested. Contribute right.</summary>
    Task<Result<RequestPreview>> PreviewRequestAsync(string stageKey, CancellationToken ct);

    /// <summary>Contribute right; <c>StageLocked</c>, <c>NoApprovers</c>, <c>ApproverCannotDecide</c>,
    /// <c>NotEnoughApprovers</c>, <c>GateRequestPending</c>, <c>GateApproved</c>.</summary>
    Task<Result<StageView>> RequestApprovalAsync(string stageKey, byte[] expectedGateVersion, CancellationToken ct);

    /// <summary>Withdraws the pending request the caller saw. Contribute right; <c>Conflict</c> when it has ended.</summary>
    Task<Result<StageView>> WithdrawAsync(string stageKey, int requestNumber, CancellationToken ct);

    /// <summary>An approver's decision: View right and a named approver who is not the excluded requester
    /// (<c>Forbidden</c> otherwise); <c>AlreadyDecided</c>; a note is required to send back; <c>Conflict</c> when the
    /// request has ended.</summary>
    Task<Result<StageView>> DecideAsync(string stageKey, int requestNumber, GateDecisionKind decision, string? note,
        CancellationToken ct);
}

public sealed record StagesView(string ProjectKey, string ProjectName, bool CanContribute, bool CanManage, bool CanRestoreDeleted,
    IReadOnlyList<StageView> Stages);

/// <param name="WaitsFor">For a locked stage, the title of the stage whose gate must be approved first.</param>
public sealed record StageView(string Key, string Title, StagePlace Place, DateOnly? StartDate, DateOnly? DueDate,
    Progress Progress, GateView Gate, byte[] StageVersion, string? WaitsFor);

/// <param name="CurrentRequest">The pending request, or the one that sent the gate back or approved it.</param>
/// <param name="CanDecide">The caller may approve or send back the pending request.</param>
public sealed record GateView(GateState State, IReadOnlyList<ApproverView> Approvers, int ApprovalsNeeded, bool RequiredIsAll,
    bool ExcludeRequester, GateRequestView? CurrentRequest, bool CanDecide, byte[] Version);

/// <param name="CanDecide">An active member of the project, who can still decide.</param>
public sealed record ApproverView(Guid UserId, string DisplayName, bool CanDecide);

public sealed record GateRequestView(int Number, string RequestedBy, Guid RequestedById, DateTimeOffset RequestedAt,
    IReadOnlyList<GateDecisionView> Decisions, string? SentBackNote);

public sealed record GateDecisionView(Guid ApproverId, string ApproverName, GateDecisionKind Decision, string? Note, DateTimeOffset At);

public sealed record RequestPreview(string StageKey, IReadOnlyList<ContainedItemView> OpenItems);

/// <summary>Someone who can be named as an approver; people outside the project become Viewers.</summary>
public sealed record ApproverOption(Guid UserId, string DisplayName, string UserName, bool IsMember);

/// <summary>A stage's gate in its drawer (FR-031).</summary>
public sealed record GateSummary(GateState State, int Approvals, int ApprovalsNeeded);
