namespace Upms.Domain.Work;

public enum GateOutcome
{
    Pending,
    Approved,
    SentBack,
    Withdrawn,

    /// <summary>The stage was deleted.</summary>
    Cancelled,
}

public enum GateDecisionKind
{
    Approve,
    SendBack,
}

/// <summary>A person on a gate's list of approvers (Phase 3 FR-035).</summary>
public sealed class GateApprover
{
    private GateApprover()
    {
    }

    public long GateId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public Guid AddedById { get; private set; }

    internal static GateApprover Create(Guid userId, ChangeContext context) =>
        new() { UserId = userId, AddedAt = context.At, AddedById = context.ActorId };
}

/// <summary>One request for a gate's approval and the decisions on it (Phase 3 FR-036–FR-038).</summary>
public sealed class GateRequest
{
    private readonly List<GateDecision> _decisions = [];

    private GateRequest()
    {
    }

    public long Id { get; private set; }

    public long GateId { get; private set; }

    /// <summary>1, 2, 3… per gate; decisions and withdrawals name the request they were made on.</summary>
    public int Number { get; private set; }

    public Guid RequestedById { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public GateOutcome Outcome { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? ClosedById { get; private set; }

    public IReadOnlyCollection<GateDecision> Decisions => _decisions;

    internal static GateRequest Create(int number, ChangeContext context) => new()
    {
        Number = number,
        RequestedById = context.ActorId,
        RequestedAt = context.At,
        Outcome = GateOutcome.Pending,
    };

    internal void Add(GateDecision decision) => _decisions.Add(decision);

    internal void Close(GateOutcome outcome, ChangeContext context)
    {
        Outcome = outcome;
        ClosedAt = context.At;
        ClosedById = context.ActorId;
    }
}

/// <summary>An approver's decision on a request, with its note; append-only (Phase 3 FR-037, FR-040).</summary>
public sealed class GateDecision
{
    private GateDecision()
    {
    }

    public long Id { get; private set; }

    public long RequestId { get; private set; }

    public Guid ApproverId { get; private set; }

    public GateDecisionKind Decision { get; private set; }

    /// <summary>Required to send back; at most 1,000 characters.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    internal static GateDecision Create(Guid approverId, GateDecisionKind decision, string? note, ChangeContext context) => new()
    {
        ApproverId = approverId,
        Decision = decision,
        Note = note,
        DecidedAt = context.At,
    };
}
