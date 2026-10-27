using Upms.Application.Common.Results;

namespace Upms.Application.Projects;

/// <summary>Deleting a space, as Jira does: it goes to the trash, leaves every list, board and search, and an Admin
/// restores it. Who may delete (owner, 2026-10-26): an Admin anywhere; with the "Delete spaces" tick, a Head in their
/// department and a Portfolio lead in the spaces they manage.</summary>
public interface ISpaceTrashService
{
    /// <summary>Whether the caller may move this space to the trash.</summary>
    Task<bool> CanDeleteAsync(string projectKey, CancellationToken ct);

    /// <summary>Moves the space to the trash; people outside it get <c>NotFound</c>.</summary>
    Task<Result> DeleteAsync(string projectKey, CancellationToken ct);

    /// <summary>The spaces in the trash, newest first: Admins only.</summary>
    Task<Result<IReadOnlyList<TrashedSpace>>> ListAsync(CancellationToken ct);

    /// <summary>Brings a space back with its work items and people: Admins only.</summary>
    Task<Result> RestoreAsync(string projectKey, CancellationToken ct);
}

public sealed record TrashedSpace(string Key, string Name, DateTimeOffset DeletedAt, string DeletedByName, int ItemCount);
