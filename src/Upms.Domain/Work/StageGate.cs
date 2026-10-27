using Upms.Domain.Common;

namespace Upms.Domain.Work;

public enum GateState
{
    NotRequested,
    AwaitingDecision,
    Approved,
    SentBack,
}

/// <summary>The approval at the end of a stage (Phase 3 FR-035–FR-038; data-model.md "Gate rules"; research R14):
/// its approvers, the approvals it needs, whether the requester is excluded, and its requests with their decisions.
/// The rules follow Jira's approvals: a set number of approvals (all approvers by default), one "send back" ends the
/// request, and an approved gate never changes. Whether the stage is current and which approvers are active members
/// are the caller's facts.</summary>
public sealed class StageGate
{
    public const int NoteMaxLength = 1_000;
    public const string RequestPendingCode = "GateRequestPending";
    public const string ApprovedCode = "GateApproved";
    public const string NoApproversCode = "NoApprovers";
    public const string ApproverCannotDecideCode = "ApproverCannotDecide";
    public const string NotEnoughApproversCode = "NotEnoughApprovers";
    public const string AlreadyDecidedCode = "AlreadyDecided";
    public const string StageLockedCode = "StageLocked";
    public const string NotApprovedCode = "GateNotApproved";

    /// <summary>The caller is not one of the approvers, or is the requester the gate excludes (Forbidden).</summary>
    public const string NotAnApproverCode = "NotAnApprover";

    /// <summary>The request the caller saw has ended (Conflict with the current gate).</summary>
    public const string RequestEndedCode = "GateRequestEnded";

    private readonly List<GateApprover> _approvers = [];
    private readonly List<GateRequest> _requests = [];

    private StageGate()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public long StageId { get; private set; }

    /// <summary>Null means all approvers (FR-035).</summary>
    public int? RequiredApprovals { get; private set; }

    public bool ExcludeRequester { get; private set; } = true;

    public GateState State { get; private set; }

    /// <summary>The latest request.</summary>
    public long? CurrentRequestId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<GateApprover> Approvers => _approvers;

    public IReadOnlyCollection<GateRequest> Requests => _requests;

    public int ApprovalsNeeded => RequiredApprovals ?? _approvers.Count;

    /// <summary>The pending request, if any.</summary>
    public GateRequest? CurrentRequest => _requests.SingleOrDefault(r => r.Outcome == GateOutcome.Pending);

    /// <summary>The note of the "send back" that ended the latest request, while the gate is sent back.</summary>
    public string? SentBackNote => State == GateState.SentBack
        ? LatestRequest?.Decisions.LastOrDefault(d => d.Decision == GateDecisionKind.SendBack)?.Note
        : null;

    public GateRequest? LatestRequest => _requests.MaxBy(r => r.Number);

    public bool IsApproved => State == GateState.Approved;

    /// <summary>A stage's gate: not requested, all approvers needed, the requester excluded (FR-035).</summary>
    public static StageGate Create(long projectId, long stageId) => new()
    {
        ProjectId = projectId,
        StageId = stageId,
        State = GateState.NotRequested,
        ExcludeRequester = true,
    };

    /// <summary>Names the approvers, the approvals needed (null for all) and the requester exclusion, while no request
    /// is pending and the gate is not approved (FR-035).</summary>
    public DomainError? SetUp(IReadOnlyCollection<Guid> approverIds, int? requiredApprovals, bool excludeRequester, ChangeContext context)
    {
        if (ChangeBlocked() is { } blocked)
        {
            return blocked;
        }

        var ids = approverIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return DomainError.Invalid("Approvers", "Name at least one approver.");
        }

        if (requiredApprovals is { } required && (required < 1 || required > ids.Count))
        {
            return DomainError.Invalid("RequiredApprovals", $"Choose from 1 to {ids.Count} approvals.");
        }

        _approvers.RemoveAll(a => !ids.Contains(a.UserId));
        foreach (var id in ids.Where(id => _approvers.All(a => a.UserId != id)))
        {
            _approvers.Add(GateApprover.Create(id, context));
        }

        RequiredApprovals = requiredApprovals;
        ExcludeRequester = excludeRequester;
        return null;
    }

    /// <summary>Requests approval of the current stage's gate (FR-036). <paramref name="activeMembers"/> are the people
    /// who can still decide: active members of the project.</summary>
    public DomainError? Request(bool isCurrentStage, IReadOnlySet<Guid> activeMembers, ChangeContext context)
    {
        if (ChangeBlocked() is { } blocked)
        {
            return blocked;
        }

        if (!isCurrentStage)
        {
            return DomainError.Rule(StageLockedCode, "Only the current stage's gate can be requested: an earlier gate must be approved first.");
        }

        if (_approvers.Count == 0)
        {
            return DomainError.Rule(NoApproversCode, "This gate has no approvers yet. Someone who manages the space names them first.");
        }

        if (_approvers.Any(a => !activeMembers.Contains(a.UserId)))
        {
            return DomainError.Rule(ApproverCannotDecideCode,
                "An approver can no longer decide: they left the space or their account was deactivated. Someone who manages the space changes the approvers first.");
        }

        if (ExcludeRequester && _approvers.Any(a => a.UserId == context.ActorId) && _approvers.Count - 1 < ApprovalsNeeded)
        {
            return DomainError.Rule(NotEnoughApproversCode,
                $"This gate needs {ApprovalsNeeded} approvals and you cannot approve your own request, so there are not enough other approvers. Someone who manages the space can add approvers or lower the number needed.");
        }

        var request = GateRequest.Create(_requests.Count == 0 ? 1 : _requests.Max(r => r.Number) + 1, context);
        _requests.Add(request);
        State = GateState.AwaitingDecision;
        return null;
    }

    /// <summary>Withdraws the pending request; its decisions no longer count (FR-036).</summary>
    public DomainError? Withdraw(int requestNumber, ChangeContext context)
    {
        if (State == GateState.Approved)
        {
            return Approved();
        }

        if (PendingRequest(requestNumber) is not { } request)
        {
            return Ended();
        }

        request.Close(GateOutcome.Withdrawn, context);
        State = GateState.NotRequested;
        return null;
    }

    /// <summary>Records an approver's decision on the pending request (FR-037, FR-038).</summary>
    public DomainError? Decide(int requestNumber, Guid approverId, GateDecisionKind decision, string? note, ChangeContext context)
    {
        if (PendingRequest(requestNumber) is not { } request)
        {
            return Ended();
        }

        if (!CanDecide(approverId, request))
        {
            return DomainError.Rule(NotAnApproverCode, "Only this gate's approvers can decide, and not the person who requested approval.");
        }

        if (request.Decisions.Any(d => d.ApproverId == approverId))
        {
            return DomainError.Rule(AlreadyDecidedCode, "You have already decided on this request.");
        }

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (decision == GateDecisionKind.SendBack && text is null)
        {
            return DomainError.Invalid("Note", "Say why you send the gate back.");
        }

        if (text is { Length: > NoteMaxLength })
        {
            return DomainError.Invalid("Note", $"A note can have at most {NoteMaxLength:N0} characters.");
        }

        request.Add(GateDecision.Create(approverId, decision, text, context));
        if (decision == GateDecisionKind.SendBack)
        {
            request.Close(GateOutcome.SentBack, context);
            State = GateState.SentBack;
        }
        else if (request.Decisions.Count(d => d.Decision == GateDecisionKind.Approve) >= ApprovalsNeeded)
        {
            request.Close(GateOutcome.Approved, context);
            State = GateState.Approved;
            ApprovedAt = context.At;
        }

        return null;
    }

    /// <summary>Whether the person may decide on the request: an approver who is not the excluded requester.</summary>
    public bool CanDecide(Guid userId, GateRequest request) =>
        _approvers.Any(a => a.UserId == userId) && !(ExcludeRequester && request.RequestedById == userId);

    /// <summary>The stage was deleted: a pending request is cancelled and the gate starts over if the stage is restored
    /// (data-model.md "Gate rules").</summary>
    public void Cancel(ChangeContext context)
    {
        CurrentRequest?.Close(GateOutcome.Cancelled, context);
        if (State != GateState.Approved)
        {
            State = GateState.NotRequested;
        }
    }

    /// <summary>Keeps <see cref="CurrentRequestId"/> on the latest request once it has an ID.</summary>
    public void TrackLatestRequest() => CurrentRequestId = LatestRequest?.Id;

    private GateRequest? PendingRequest(int number) => CurrentRequest is { } pending && pending.Number == number ? pending : null;

    private DomainError? ChangeBlocked() => State switch
    {
        GateState.Approved => Approved(),
        GateState.AwaitingDecision => DomainError.Rule(RequestPendingCode, "Approval has been requested. Withdraw the request first."),
        _ => null,
    };

    private static DomainError Approved() => DomainError.Rule(ApprovedCode, "This gate is approved, which is final.");

    private static DomainError Ended() => DomainError.Rule(RequestEndedCode, "This request has ended. The gate's current state is shown.");
}
