using Upms.Domain.Common;

namespace Upms.Application.Common;

/// <summary>The caller's current status, read from the database on every call so that a
/// deactivation or role change applies at once (research R7).</summary>
public interface ICallerContext
{
    /// <summary>The caller, or null when nobody is signed in or the account no longer exists.</summary>
    Task<CallerStatus?> GetAsync(CancellationToken ct);
}

/// <summary>A signed-in caller.</summary>
/// <param name="Type">Which of the five kinds of user they are (owner's decision of 2026-10-20).</param>
/// <param name="DepartmentId">Their department; a Head sees all of it.</param>
/// <param name="Rights">Their ticks: their type's, plus their own, less those taken away; every one for an Admin.</param>
public sealed record CallerStatus(Guid UserId, bool IsActive, bool IsAdministrator, UserType Type = UserType.Member,
    long? DepartmentId = null, Rights Rights = Rights.None)
{
    /// <summary>True for an active caller with the tick, and always for an administrator.</summary>
    public bool Can(Rights right) => IsActive && (IsAdministrator || Rights.Has(right));
}
