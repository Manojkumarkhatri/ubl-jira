using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A Scrum project's backlog and sprints as one ordered list (Phase 3 FR-021, FR-022, FR-024, FR-026). Other
/// projects have no backlog: <c>NotFound</c>.</summary>
public interface IBacklogService
{
    /// <summary>The active and planned sprints with their cards, then a page of the backlog: 100 open cards by rank.
    /// View right.</summary>
    Task<Result<BacklogView>> GetAsync(string projectKey, PageRequest backlogPage, CancellationToken ct);

    /// <summary>Reorders a card or moves it between the backlog and sprints; its sub-tasks follow it. Contribute right;
    /// <c>SprintCompleted</c>; <c>Conflict</c> with the current row when the card changed meanwhile.</summary>
    Task<Result<BacklogRow>> MoveAsync(string workItemKey, BacklogTarget target, BacklogPlacement placement,
        byte[] expectedVersion, CancellationToken ct);
}

/// <summary>The list a card goes to.</summary>
public abstract record BacklogTarget
{
    public static BacklogTarget ToBacklog { get; } = new Backlog();

    public sealed record Backlog : BacklogTarget;

    public sealed record Sprint(long SprintId) : BacklogTarget;
}

/// <summary>Where the card goes in the target list, as <see cref="CardPlacement"/> does on the board. Keep: the card keeps
/// its rank and joins the list where that rank falls ("Move to MOB Sprint 2"). Before: drag and drop.</summary>
public abstract record BacklogPlacement
{
    public static BacklogPlacement KeepRank { get; } = new Keep();

    public static BacklogPlacement AtTop { get; } = new Top();

    public static BacklogPlacement AtBottom { get; } = new Bottom();

    public static BacklogPlacement OneUp { get; } = new Up();

    public static BacklogPlacement OneDown { get; } = new Down();

    public sealed record Keep : BacklogPlacement;

    public sealed record Top : BacklogPlacement;

    public sealed record Bottom : BacklogPlacement;

    public sealed record Up : BacklogPlacement;

    public sealed record Down : BacklogPlacement;

    public sealed record Before(string WorkItemKey) : BacklogPlacement;
}

/// <param name="Backlog">Open cards in no sprint, or in a completed one, by rank: 100 per page.</param>
/// <param name="Completed">Past sprints, newest first, linking to their reports (FR-029).</param>
public sealed record BacklogView(string ProjectKey, string ProjectName, bool CanContribute, bool CanManageSprints,
    bool CanRestoreDeleted, SprintSection? Active, IReadOnlyList<SprintSection> Planned, Page<BacklogRow> Backlog,
    IReadOnlyList<CompletedSprintLink> Completed, IReadOnlyList<StatusOption> Statuses,
    WorkTypeSet WorkTypes = WorkTypeSet.All);

/// <summary>A planned or active sprint with its cards in rank order (FR-026).</summary>
public sealed record SprintSection(long Id, int Number, string Name, string? Goal, SprintState State, DateOnly? StartDate,
    DateOnly? EndDate, SprintTotals Totals, IReadOnlyList<BacklogRow> Items, byte[] Version);

/// <summary>Unestimated cards count as 0 (spec edge case). Completed and remaining matter for the active sprint.</summary>
public sealed record SprintTotals(int Items, decimal Estimate, int Unestimated, decimal CompletedEstimate, decimal RemainingEstimate)
{
    public static SprintTotals Of(IReadOnlyCollection<BacklogRow> rows) => new(
        rows.Count,
        rows.Sum(r => r.Estimate ?? 0),
        rows.Count(r => r.Estimate is null),
        rows.Where(r => r.Status.Category == Domain.Common.StatusCategory.Done).Sum(r => r.Estimate ?? 0),
        rows.Where(r => r.Status.Category != Domain.Common.StatusCategory.Done).Sum(r => r.Estimate ?? 0));
}

public sealed record BacklogRow(string Key, WorkItemType Type, string Title, ContainerView? Epic, AssigneeRef? Assignee,
    decimal? Estimate, StatusOption Status, int SubtaskCount, byte[] Version);

public sealed record CompletedSprintLink(int Number, string Name, DateOnly? EndDate);
