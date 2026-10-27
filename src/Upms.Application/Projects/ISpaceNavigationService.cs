using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Projects;

/// <summary>The sidebar's spaces: starred ones and those opened last (Jira parity pass FR-021, FR-021b; research R6, R7).
/// Only spaces the caller can see now are listed.</summary>
public interface ISpaceNavigationService
{
    const int RecentSpaces = 5;
    const int SidebarSpaces = 8;

    /// <summary>Starred spaces by name, the 5 opened last, and "Spaces": starred ones, then the ones opened last, up to 8.</summary>
    Task<Result<SidebarView>> GetSidebarAsync(CancellationToken ct);

    /// <summary>View right; starring twice is the same as once.</summary>
    Task<Result> StarAsync(string spaceKey, CancellationToken ct);

    Task<Result> UnstarAsync(string spaceKey, CancellationToken ct);

    /// <summary>View right; called once when a space page opens. Each person keeps their latest 20 visits.</summary>
    Task<Result> RecordVisitAsync(string spaceKey, CancellationToken ct);

    /// <summary>The caller's starred spaces by name, for "For you".</summary>
    Task<Result<IReadOnlyList<SpaceLink>>> StarredAsync(CancellationToken ct);
}

public sealed record SidebarView(IReadOnlyList<SpaceLink> Starred, IReadOnlyList<SpaceLink> RecentSpaces, IReadOnlyList<SpaceLink> Spaces,
    bool IsAdministrator);

public sealed record SpaceLink(string Key, string Name, ProjectTemplate Template, bool IsStarred);
