namespace Upms.Application.Projects.Contracts;

/// <summary>Keeps space access in step with people's types, ticks, departments and portfolios (owner's decision of
/// 2026-10-20). Access is kept as memberships, so every page that lists a person's spaces sees it: a portfolio's leads and
/// its department's Heads are members of its spaces, and each member's role follows their ticks. Runs in the caller's
/// transaction.</summary>
public interface ISpaceAccessSync
{
    /// <summary>After these people's type, department or ticks changed: their roles in the spaces they were added to, and the
    /// access their department's spaces give Heads.</summary>
    Task SyncPeopleAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>After these spaces moved portfolio, or their portfolio's leads changed: who the portfolio and department
    /// give access to.</summary>
    Task SyncSpacesAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct);
}
