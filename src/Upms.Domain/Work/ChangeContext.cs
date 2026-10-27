namespace Upms.Domain.Work;

/// <summary>Who changes work items, when, and which user action groups the changes (change set).</summary>
public sealed record ChangeContext(Guid ActorId, DateTimeOffset At, Guid ChangeSetId)
{
    public static ChangeContext New(Guid actorId, DateTimeOffset at) => new(actorId, at, Guid.NewGuid());
}

/// <summary>A person as the Work domain sees it: enough to name them in history (Phase 2 FR-023).</summary>
public sealed record PersonRef(Guid Id, string DisplayName);

/// <summary>An epic or stage as the Work domain sees it when placing an item in it: enough to check the rules and to
/// name it in history (Phase 3 FR-015).</summary>
public sealed record ContainerRef(long Id, long ProjectId, string Key, string Title, WorkItemType Type)
{
    /// <summary>"MOB-1 Card refunds", the snapshot history keeps.</summary>
    public string Label => $"{Key} {Title}";
}

/// <summary>A sprint as a card sees it when joining or leaving it: enough to check the rules and to name it in history
/// (Phase 3 FR-024, FR-030).</summary>
public sealed record SprintRef(long Id, string Name, bool IsCompleted);

/// <summary>A project status as the Work domain sees it: enough to name it in history and to know
/// whether it completes the work.</summary>
public sealed record StatusRef(long Id, string Name, Upms.Domain.Common.StatusCategory Category)
{
    public bool IsDone => Category == Upms.Domain.Common.StatusCategory.Done;
}
