using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The Kanban board (FR-016 to FR-023; Phase 2 FR-003, FR-021, FR-022).</summary>
public interface IBoardService
{
    /// <param name="showAllDone">False shows only tasks completed in the last 14 days in "done" columns (FR-021).</param>
    Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct);

    /// <summary>The board with its view options; <see cref="BoardRead.ShowEpics"/> adds epics as cards on a Kanban board.</summary>
    Task<Result<BoardView>> GetAsync(string projectKey, BoardRead read, CancellationToken ct);

    /// <summary>"What needs to be done?": a new card at the bottom of the column (FR-018), or in a Scrum project after
    /// every card (Phase 3 research R8). Only stories, tasks and bugs the template offers (<c>TypeNotAllowed</c>, Phase 3
    /// FR-010).</summary>
    Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title, WorkItemType type, CancellationToken ct);

    /// <summary>Moves or reorders a card; <c>Conflict</c> when someone else changed it first (FR-019, FR-022). A sub-task card
    /// changes column only, keeping its place under its parent. With <paramref name="lane"/>, the card also changes epic,
    /// stage or assignee, in the same change and by the drawer's rules (Jira parity pass FR-027; research R17, R18).</summary>
    Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement, byte[] expectedVersion,
        LaneChange? lane, CancellationToken ct);
}

/// <param name="ShowAllDone">False shows only tasks completed in the last 14 days in "done" columns (FR-021).</param>
/// <param name="ShowEpics">Epics as cards too, on a Kanban board. Jira's team-managed boards never show them (JRACLOUD-85704); people
/// asked for it, so it is a view option.</param>
public sealed record BoardRead(bool ShowAllDone, bool ShowEpics = false);

/// <summary>The lane a card is dropped in, when it is not its own (Jira parity pass FR-027).</summary>
public abstract record LaneChange
{
    private LaneChange()
    {
    }

    /// <summary>An epic's or stage's lane by key, or null for "No epic" or "No stage" (<c>SubtaskFollowsParent</c> for a
    /// sub-task, whose epic is its parent's).</summary>
    public sealed record Container(string? ContainerKey) : LaneChange;

    /// <summary>A person's lane, or null for "Unassigned" (<c>NotAssignable</c>).</summary>
    public sealed record Assignee(Guid? UserId) : LaneChange;

    /// <summary>A priority's lane: the card takes that priority.</summary>
    public sealed record Priority(Domain.Work.Priority Value) : LaneChange;
}

/// <summary>Where a moved card goes in its column (research R14, R15).</summary>
public abstract record CardPlacement
{
    private CardPlacement()
    {
    }

    public static CardPlacement AtTop { get; } = new Top();

    public static CardPlacement AtEnd { get; } = new End();

    /// <summary>Dropped on a card: insert before it.</summary>
    public sealed record Before(string WorkItemKey) : CardPlacement;

    /// <summary>Dropped on the column footer, or "bottom" in the "Move to" menu.</summary>
    public sealed record End : CardPlacement;

    /// <summary>"Top" in the "Move to" menu.</summary>
    public sealed record Top : CardPlacement;

    /// <summary>A "Lane" in the "Move to" menu: the card keeps its place (Jira parity pass FR-027).</summary>
    public static CardPlacement InPlace { get; } = new Keep();

    public sealed record Keep : CardPlacement;
}

public sealed record BoardView(
    string ProjectKey,
    string ProjectName,
    int BoardVersion,
    bool CanManageColumns,
    bool ShowingAllDone,
    int HiddenDoneCount,
    IReadOnlyList<ColumnView> Columns,
    Guid ViewerId,
    bool CanRestoreDeleted = false,
    bool CanContribute = false,
    ProjectTemplate Template = ProjectTemplate.Kanban,
    ActiveSprintInfo? Sprint = null,
    bool CanManageSprints = false,
    WorkTypeSet WorkTypes = WorkTypeSet.All);

/// <summary>The sprint a Scrum board shows (Phase 3 FR-027); a Scrum board without one shows no cards.</summary>
/// <param name="Destinations">The planned sprints its open cards can go to when it is completed (FR-028).</param>
public sealed record ActiveSprintInfo(long Id, int Number, string Name, string? Goal, DateOnly StartDate, DateOnly EndDate,
    byte[] Version, IReadOnlyList<SprintOption> Destinations);

public sealed record SprintOption(long Id, string Name);

/// <param name="CardCount">The number of cards the column shows.</param>
/// <param name="OverLimit">True when <paramref name="CardCount"/> exceeds <paramref name="WipLimit"/> (FR-036).</param>
public sealed record ColumnView(
    long Id,
    string Name,
    StatusCategory Category,
    int? WipLimit,
    int CardCount,
    bool OverLimit,
    IReadOnlyList<CardView> Cards);

/// <param name="Version">The row version the card was read with; moves send it back (FR-022).</param>
/// <param name="Assignee">Shown as initials with the name as text (Phase 2 FR-021, FR-024).</param>
/// <param name="Type">Story, Task or Bug, shown as a symbol with its name as text (Phase 3 FR-009).</param>
/// <param name="Estimate">Shown when set (Phase 3 FR-014).</param>
/// <param name="Container">The epic or stage the card is in, for its tag and the quick filters (Phase 4 FR-047); a sub-task's is
/// its parent's.</param>
/// <param name="ParentKey">A sub-task card's parent (Jira parity pass FR-027a).</param>
public sealed record CardView(
    string Key,
    string Title,
    Priority Priority,
    long ColumnId,
    int SubtasksDone,
    int SubtasksTotal,
    byte[] Version,
    AssigneeRef? Assignee,
    DateOnly? DueDate,
    WorkItemType Type = WorkItemType.Task,
    decimal? Estimate = null,
    IReadOnlyList<string>? Labels = null,
    ContainerView? Container = null,
    string? ParentKey = null)
{
    /// <summary>The card's labels, for the card and the quick filters (Phase 4 FR-045, FR-047).</summary>
    public IReadOnlyList<string> Labels { get; init; } = Labels ?? [];

    /// <summary>Jira's card cover: a colour name, or an image attachment shown at the top of the card.</summary>
    public string? CoverColor { get; init; }

    public long? CoverImageId { get; init; }
}
