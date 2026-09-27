using Upms.Application.Common;
using Upms.Application.Common.Results;

namespace Upms.Application.Projects;

/// <summary>Projects (FR-011 to FR-015).</summary>
public interface IProjectService
{
    /// <summary>All projects sorted by name, a page at a time (FR-013).</summary>
    Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct);

    /// <summary>A key suggested from the name that is not in use yet (FR-011).</summary>
    Task<string> SuggestKeyAsync(string projectName, CancellationToken ct);

    /// <summary>Creates a project owned by the caller with the default columns; returns its key.</summary>
    Task<Result<string>> CreateAsync(string name, string key, string? description, CancellationToken ct);

    Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct);

    /// <summary>Owner and Administrators (FR-014); <c>Conflict</c> when the details changed meanwhile.</summary>
    Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description,
        int expectedDetailsVersion, CancellationToken ct);
}

public sealed record ProjectSummary(string Key, string Name, string OwnerDisplayName, int OpenItemCount);

public sealed record ProjectDetails(
    string Key,
    string Name,
    string? Description,
    string OwnerDisplayName,
    bool CanManage,
    int DetailsVersion,
    DateTimeOffset CreatedAt);
