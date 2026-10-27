using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>An item's watchers (Phase 4 FR-011–FR-014, research R7). Watching is a personal subscription: anyone who
/// can see the item starts or stops it for themselves; Project Admins and administrators add and remove others.</summary>
public interface IWatcherService
{
    Task<Result<WatchersView>> GetAsync(string workItemKey, CancellationToken ct);

    /// <summary>Idempotent.</summary>
    Task<Result<WatchersView>> WatchAsync(string workItemKey, CancellationToken ct);

    /// <summary>Idempotent.</summary>
    Task<Result<WatchersView>> StopWatchingAsync(string workItemKey, CancellationToken ct);

    /// <summary>Manage right; an active member of the project only (<c>NotAMember</c>).</summary>
    Task<Result<WatchersView>> AddAsync(string workItemKey, Guid userId, CancellationToken ct);

    /// <summary>Manage right.</summary>
    Task<Result<WatchersView>> RemoveAsync(string workItemKey, Guid userId, CancellationToken ct);
}

/// <summary>The people watching an item who can still see it, by name.</summary>
/// <param name="CanManage">The caller may add and remove other people.</param>
public sealed record WatchersView(int Count, bool IsWatching, IReadOnlyList<PersonView> People, bool CanManage);

/// <summary>A person as suggestions and lists show them.</summary>
public sealed record PersonView(Guid UserId, string DisplayName, string UserName);
