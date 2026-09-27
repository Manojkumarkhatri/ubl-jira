namespace Upms.Application.Common;

/// <summary>The caller's current status, read from the database on every call so that a
/// deactivation or role change applies at once (research R7).</summary>
public interface ICallerContext
{
    /// <summary>The caller, or null when nobody is signed in or the account no longer exists.</summary>
    Task<CallerStatus?> GetAsync(CancellationToken ct);
}

/// <summary>A signed-in caller.</summary>
public sealed record CallerStatus(Guid UserId, bool IsActive, bool IsAdministrator);
