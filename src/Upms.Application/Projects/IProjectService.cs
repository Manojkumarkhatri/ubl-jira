using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Projects (FR-011 to FR-015; Phase 2 FR-002, FR-007, FR-015).</summary>
public interface IProjectService
{
    /// <summary>The projects the caller can see (their own; every project for administrators), sorted by name, a page
    /// at a time (Phase 2 FR-015).</summary>
    Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct);

    /// <summary>A key suggested from the name that is not in use yet (FR-011).</summary>
    Task<string> SuggestKeyAsync(string projectName, CancellationToken ct);

    /// <summary>Creates a project with the default columns and the caller as its first Project Admin; returns its key.</summary>
    Task<Result<string>> CreateAsync(string name, string key, string? description, CancellationToken ct);

    Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct);

    /// <summary>Project Admins and Administrators (FR-014); <c>Conflict</c> when the details changed meanwhile.</summary>
    Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description,
        int expectedDetailsVersion, CancellationToken ct);
}

/// <param name="MyRole">The caller's role in the project; null when they see it only as an administrator.</param>
/// <param name="AdministratorAccess">True when the caller is an administrator who is not a member.</param>
public sealed record ProjectSummary(string Key, string Name, ProjectRole? MyRole, bool AdministratorAccess, int OpenItemCount);

public sealed record ProjectDetails(
    string Key,
    string Name,
    string? Description,
    string OwnerDisplayName,
    bool CanManage,
    int DetailsVersion,
    DateTimeOffset CreatedAt);
