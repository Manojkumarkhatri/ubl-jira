using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Projects;

/// <summary>A board's columns (FR-034 to FR-039): Project Admins and Administrators only. Every change
/// carries the board version it was based on; a stale one returns <c>Conflict</c> with the current columns
/// (FR-041).</summary>
public interface IBoardColumnService
{
    Task<Result<BoardColumnsView>> GetAsync(string projectKey, CancellationToken ct);

    /// <summary>Adds a column at a 0-based position (<c>DuplicateColumnName</c>, <c>TooManyColumns</c>).</summary>
    Task<Result<BoardColumnsView>> AddAsync(string projectKey, string name, StatusCategory category, int position,
        int expectedBoardVersion, CancellationToken ct);

    Task<Result<BoardColumnsView>> RenameAsync(string projectKey, long columnId, string name, int expectedBoardVersion,
        CancellationToken ct);

    Task<Result<BoardColumnsView>> MoveAsync(string projectKey, long columnId, int newPosition, int expectedBoardVersion,
        CancellationToken ct);

    /// <summary>A limit of 1–99, or null for none (FR-036).</summary>
    Task<Result<BoardColumnsView>> SetWipLimitAsync(string projectKey, long columnId, int? limit, int expectedBoardVersion,
        CancellationToken ct);

    /// <summary>Only while the column is empty (<c>ColumnNotEmpty</c>, <c>LastToDoColumn</c>, <c>LastDoneColumn</c>).</summary>
    Task<Result<BoardColumnsView>> ChangeCategoryAsync(string projectKey, long columnId, StatusCategory category,
        int expectedBoardVersion, CancellationToken ct);

    /// <summary>Moves the column's work items to the destination, then removes it (<c>DestinationRequired</c>,
    /// <c>LastToDoColumn</c>, <c>LastDoneColumn</c>).</summary>
    Task<Result<BoardColumnsView>> DeleteAsync(string projectKey, long columnId, long? destinationColumnId,
        int expectedBoardVersion, CancellationToken ct);
}

public sealed record BoardColumnsView(string ProjectKey, string ProjectName, int BoardVersion, IReadOnlyList<BoardColumnView> Columns)
{
    public const int MaxColumns = Domain.Projects.Project.MaxColumns;
}

/// <param name="ItemCount">Work items in the column that are not deleted: tasks and sub-tasks.</param>
/// <param name="IsEmpty">No work item has this status, deleted ones included (FR-039).</param>
public sealed record BoardColumnView(long Id, string Name, StatusCategory Category, int Position, int? WipLimit, int ItemCount,
    bool IsEmpty);
