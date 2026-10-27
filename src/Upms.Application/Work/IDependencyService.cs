using Upms.Application.Common.Results;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>"B waits for A" between items of one project (Phase 3 FR-047–FR-052). Adding and removing need the Contribute
/// right on the project of both items; an item of another project is <c>NotFound</c>. Dates never change because of a
/// dependency.</summary>
public interface IDependencyService
{
    /// <summary>Records that <paramref name="waitingKey"/> waits for <paramref name="waitedForKey"/>. <c>DependencyToSelf</c>,
    /// <c>DuplicateDependency</c>, <c>DependencyInHierarchy</c>, <c>DependencyLoop</c>. Answers with the waiting item's
    /// lists.</summary>
    Task<Result<DependencyList>> AddAsync(string waitingKey, string waitedForKey, CancellationToken ct);

    /// <summary>Removes the dependency; answers with the waiting item's lists.</summary>
    Task<Result<DependencyList>> RemoveAsync(string waitingKey, string waitedForKey, CancellationToken ct);

    /// <summary>Items of the project by key or words in the title, at most 20. View right.</summary>
    Task<Result<IReadOnlyList<ItemOption>>> FindItemsAsync(string projectKey, string term, CancellationToken ct);
}

/// <summary>What an item waits for, and what waits for it (FR-049).</summary>
public sealed record DependencyList(IReadOnlyList<DependencyView> WaitsFor, IReadOnlyList<DependencyView> Blocks)
{
    public static DependencyList Empty { get; } = new([], []);
}

/// <param name="IsLate">The waiting item starts before the item it waits for is due, while that item is open (FR-051).</param>
public sealed record DependencyView(string Key, WorkItemType Type, string Title, StatusOption Status, DateOnly? StartDate,
    DateOnly? DueDate, bool IsLate);

public sealed record ItemOption(string Key, WorkItemType Type, string Title);
