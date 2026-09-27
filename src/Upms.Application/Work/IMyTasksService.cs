using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>"My tasks": the caller's open tasks and sub-tasks in the projects they can see (Phase 2 FR-025, FR-026).</summary>
public interface IMyTasksService
{
    /// <summary>Ordered by project name, then due date (undated last), priority and number.</summary>
    Task<Result<Page<MyTaskRow>>> ListAsync(PageRequest page, CancellationToken ct);
}

/// <param name="ParentKey">The parent task of a sub-task.</param>
public sealed record MyTaskRow(string ProjectKey, string ProjectName, string Key, string Title, string? ParentKey,
    StatusOption Status, Priority Priority, DateOnly? DueDate);
