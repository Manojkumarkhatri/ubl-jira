using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The Kanban board (FR-016 to FR-023; Phase 2 FR-003, FR-021, FR-022).</summary>
public interface IBoardService
{
    /// <param name="showAllDone">False shows only tasks completed in the last 14 days in "done" columns (FR-021).</param>
    Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct);

    /// <summary>"What needs to be done?": a new task at the bottom of the column (FR-018).</summary>
    Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title, CancellationToken ct);

    /// <summary>Moves or reorders a card; <c>Conflict</c> when someone else changed it first (FR-019, FR-022).</summary>
    Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement, byte[] expectedVersion,
        CancellationToken ct);
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
    bool CanContribute = false);

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
public sealed record CardView(
    string Key,
    string Title,
    Priority Priority,
    long ColumnId,
    int SubtasksDone,
    int SubtasksTotal,
    byte[] Version,
    AssigneeRef? Assignee,
    DateOnly? DueDate);
