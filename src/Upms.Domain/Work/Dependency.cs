using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>"B waits for A" between two work items of one project (Phase 3 FR-047): finish-to-start, never moving any
/// date. Removing a link deletes it; both items' history keeps the record (research R18).</summary>
public sealed class Dependency
{
    private Dependency()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    /// <summary>The item waited for (A).</summary>
    public long PredecessorId { get; private set; }

    /// <summary>The waiting item (B).</summary>
    public long SuccessorId { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Dependency Create(long projectId, long predecessorId, long successorId, ChangeContext context) => new()
    {
        ProjectId = projectId,
        PredecessorId = predecessorId,
        SuccessorId = successorId,
        CreatedById = context.ActorId,
        CreatedAt = context.At,
    };
}

/// <summary>An item as the dependency rules see it: its parent (for a sub-task) and its epic or stage.</summary>
public sealed record DependencyNode(long Id, long? ParentId, long? ContainerId);

/// <summary>The rules of a project's dependencies (Phase 3 FR-048, FR-051; research R18, R19).</summary>
public static class DependencyGraph
{
    public const string ToSelfCode = "DependencyToSelf";
    public const string DuplicateCode = "DuplicateDependency";
    public const string InHierarchyCode = "DependencyInHierarchy";
    public const string LoopCode = "DependencyLoop";

    /// <summary>Whether <paramref name="waiting"/> may wait for <paramref name="waitedFor"/>, given the project's links as
    /// (item waited for, waiting item) pairs.</summary>
    public static DomainError? CheckNew(DependencyNode waiting, DependencyNode waitedFor,
        IReadOnlyCollection<(long Predecessor, long Successor)> links)
    {
        if (waiting.Id == waitedFor.Id)
        {
            return DomainError.Rule(ToSelfCode, "An item cannot wait for itself.");
        }

        if (links.Contains((waitedFor.Id, waiting.Id)))
        {
            return DomainError.Rule(DuplicateCode, "This dependency already exists.");
        }

        if (waiting.ParentId == waitedFor.Id || waitedFor.ParentId == waiting.Id
            || waiting.ContainerId == waitedFor.Id || waitedFor.ContainerId == waiting.Id)
        {
            return DomainError.Rule(InHierarchyCode,
                "An item cannot wait for its own epic, stage, parent or sub-task, or be waited for by them.");
        }

        return Reaches(waiting.Id, waitedFor.Id, links)
            ? DomainError.Rule(LoopCode, "This would create a loop: the other item already waits for this one, directly or through other items.")
            : null;
    }

    /// <summary>Late while the item waited for is open and the waiting item starts before it is due (FR-051). The
    /// waiting item's start is its start date, or its due date without one, as for a milestone. Missing dates are never
    /// late.</summary>
    public static bool IsLate(bool waitedForOpen, DateOnly? waitedForDue, DateOnly? waitingStart, DateOnly? waitingDue) =>
        waitedForOpen && waitedForDue is { } due && (waitingStart ?? waitingDue) is { } start && start < due;

    /// <summary>Whether items wait, directly or through others, for <paramref name="from"/> all the way to
    /// <paramref name="to"/> (a breadth-first search along "is waited for by").</summary>
    private static bool Reaches(long from, long to, IReadOnlyCollection<(long Predecessor, long Successor)> links)
    {
        var next = links.ToLookup(l => l.Predecessor, l => l.Successor);
        var seen = new HashSet<long> { from };
        var queue = new Queue<long>([from]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var successor in next[current])
            {
                if (successor == to)
                {
                    return true;
                }

                if (seen.Add(successor))
                {
                    queue.Enqueue(successor);
                }
            }
        }

        return false;
    }
}
