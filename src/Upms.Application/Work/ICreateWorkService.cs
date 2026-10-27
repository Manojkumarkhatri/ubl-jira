using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>What the Create form offers (Jira parity pass FR-002–FR-005; research R10). The item itself is created by
/// <see cref="IWorkItemService.CreateAsync"/>.</summary>
public interface ICreateWorkService
{
    /// <summary>The spaces the caller can add work to, by name, each with the types offered to the caller; the default is
    /// <paramref name="currentSpaceKey"/> when it is one of them, else the space the caller last created an item in, else
    /// the first.</summary>
    Task<Result<CreateSpaces>> SpacesAsync(string? currentSpaceKey, CancellationToken ct);

    /// <summary>The space's types, columns, people who can be assigned, epics or stages, and active and planned sprints.</summary>
    Task<Result<CreateOptions>> OptionsAsync(string spaceKey, CancellationToken ct);

    /// <summary>Up to 20 possible parents by key or title: epics or stages for standard items, stories, tasks and bugs for
    /// sub-tasks.</summary>
    Task<Result<IReadOnlyList<ParentOption>>> FindParentsAsync(string spaceKey, WorkItemType type, string text, CancellationToken ct);
}

public sealed record CreateSpaces(IReadOnlyList<CreateSpaceOption> Spaces, string? DefaultKey);

/// <param name="Types">The types the caller may create here, in the order the form lists them.</param>
public sealed record CreateSpaceOption(string Key, string Name, ProjectTemplate Template, IReadOnlyList<WorkItemType> Types);

public sealed record CreateOptions(
    string SpaceKey,
    ProjectTemplate Template,
    IReadOnlyList<WorkItemType> Types,
    IReadOnlyList<StatusOption> Columns,
    IReadOnlyList<AssigneeOption> Assignees,
    IReadOnlyList<ContainerOption> Containers,
    IReadOnlyList<SprintOption> Sprints);

public sealed record ParentOption(string Key, string Title, WorkItemType Type);
