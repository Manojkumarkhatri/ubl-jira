namespace Upms.Application.Projects.Contracts;

/// <summary>Allocates the next work item number of a project atomically inside the current transaction,
/// so concurrent creations never share a key and keys are never reused (FR-024, research R13).</summary>
public interface IWorkItemNumberAllocator
{
    Task<int> NextAsync(long projectId, CancellationToken ct);
}
