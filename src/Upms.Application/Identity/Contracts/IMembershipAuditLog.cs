namespace Upms.Application.Identity.Contracts;

/// <summary>Records project membership changes in the security audit log for the Projects module (Phase 2 FR-012),
/// so that module does not depend on the Identity module's audit event types.</summary>
public interface IMembershipAuditLog
{
    /// <param name="details">Serialized into the event, for example the old and new roles; never secrets.</param>
    Task WriteAsync(MembershipChange change, Guid memberId, string projectKey, object details, CancellationToken ct);
}

public enum MembershipChange
{
    Added,
    Removed,
    RoleChanged,
}
