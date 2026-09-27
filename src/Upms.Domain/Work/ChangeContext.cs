namespace Upms.Domain.Work;

/// <summary>Who changes work items, when, and which user action groups the changes (change set).</summary>
public sealed record ChangeContext(Guid ActorId, DateTimeOffset At, Guid ChangeSetId)
{
    public static ChangeContext New(Guid actorId, DateTimeOffset at) => new(actorId, at, Guid.NewGuid());
}

/// <summary>A person as the Work domain sees it: enough to name them in history (Phase 2 FR-023).</summary>
public sealed record PersonRef(Guid Id, string DisplayName);

/// <summary>A project status as the Work domain sees it: enough to name it in history and to know
/// whether it completes the work.</summary>
public sealed record StatusRef(long Id, string Name, Upms.Domain.Common.StatusCategory Category)
{
    public bool IsDone => Category == Upms.Domain.Common.StatusCategory.Done;
}
