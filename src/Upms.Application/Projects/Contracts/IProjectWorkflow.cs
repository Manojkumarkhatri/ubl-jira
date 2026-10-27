using Upms.Domain.Common;

namespace Upms.Application.Projects.Contracts;

/// <summary>A project's columns (statuses), for the Work module (research R11).</summary>
public interface IProjectWorkflow
{
    Task<BoardInfo> GetBoardInfoAsync(long projectId, CancellationToken ct);

    /// <summary>The project's statuses in position order.</summary>
    Task<IReadOnlyList<StatusInfo>> StatusesAsync(long projectId, CancellationToken ct);

    /// <summary>The statuses of several projects at once, by project ID ("My tasks", Phase 2 research R13).</summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<StatusInfo>>> StatusesAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct);

    /// <summary>The statuses in these categories, in these projects or in every project (search, Phase 4 FR-035).</summary>
    Task<IReadOnlyList<long>> StatusIdsAsync(IReadOnlyCollection<StatusCategory> categories, IReadOnlyCollection<long>? projectIds,
        CancellationToken ct);

    /// <summary>The leftmost "to do" status, where new sub-tasks start (FR-040).</summary>
    Task<StatusInfo> FirstToDoStatusAsync(long projectId, CancellationToken ct);
}

public sealed record StatusInfo(long Id, string Name, StatusCategory Category, int Position, int? WipLimit);

/// <param name="Template">How the project works (Phase 3 research R2).</param>
public sealed record BoardInfo(long ProjectId, string Key, string Name, int BoardVersion, IReadOnlyList<StatusInfo> Statuses,
    ProjectTemplate Template = ProjectTemplate.Kanban);
