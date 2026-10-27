namespace Upms.Application.Identity.Contracts;

/// <summary>Records template switches and gate events in the security audit log for the Projects and Work modules
/// (Phase 3 FR-005, FR-040, research R17), so those modules do not depend on the Identity module's audit event types.
/// The actor is the current user.</summary>
public interface IPlanningAuditLog
{
    /// <param name="target">The project key for a template switch; the stage key for a gate event.</param>
    /// <param name="details">Serialized into the event, for example the old and new template; never secrets.</param>
    Task WriteAsync(PlanningAuditEvent change, string target, object details, CancellationToken ct);
}

public enum PlanningAuditEvent
{
    TemplateChanged,
    GateSetupChanged,
    GateApprovalRequested,
    GateRequestWithdrawn,
    GateDecisionRecorded,
    GateApproved,
    WorkTypesChanged,
}
