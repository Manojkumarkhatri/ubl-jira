using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>An in-memory board for component tests; records every call.</summary>
public sealed class FakeBoardService : IBoardService
{
    public const long ToDo = 1;
    public const long InProgress = 2;
    public const long Done = 3;

    /// <summary>The signed-in person the board is shown to.</summary>
    public static readonly Guid Viewer = Guid.NewGuid();

    public FakeBoardService()
    {
        Board = new BoardView("WEB", "Website Revamp", 1, CanManageColumns: true, ShowingAllDone: false, HiddenDoneCount: 0,
        [
            new ColumnView(ToDo, "To Do", StatusCategory.ToDo, null, 2, false,
            [
                Card("WEB-1", "Design the home page", Priority.High, ToDo),
                Card("WEB-2", "Write the copy", Priority.Medium, ToDo),
            ]),
            new ColumnView(InProgress, "In Progress", StatusCategory.InProgress, null, 1, false,
            [
                Card("WEB-3", "Pick the colours", Priority.Low, InProgress),
            ]),
            new ColumnView(Done, "Done", StatusCategory.Done, null, 0, false, []),
        ], Viewer, CanContribute: true);
    }

    public BoardView Board { get; set; }

    public List<(string Key, long ColumnId, CardPlacement Placement, byte[] Version)> Moves { get; } = [];

    public List<(long ColumnId, string Title)> Created { get; } = [];

    public Func<Result<CardView>>? NextMoveResult { get; set; }

    public Func<Result<CardView>>? NextCreateResult { get; set; }

    public int Loads { get; private set; }

    public static CardView Card(string key, string title, Priority priority, long columnId, AssigneeRef? assignee = null,
        DateOnly? dueDate = null) =>
        new(key, title, priority, columnId, 0, 0, [1, 2, 3], assignee, dueDate);

    public Task<Result<BoardView>> GetAsync(string projectKey, bool showAllDone, CancellationToken ct)
    {
        Loads++;
        return Task.FromResult(projectKey == Board.ProjectKey
            ? Result<BoardView>.Ok(Board with { ShowingAllDone = showAllDone })
            : Result<BoardView>.Fail(AppError.NotFound("project")));
    }

    public Task<Result<CardView>> CreateInlineAsync(string projectKey, long columnId, string title, CancellationToken ct)
    {
        Created.Add((columnId, title));
        return Task.FromResult(NextCreateResult?.Invoke() ?? Result<CardView>.Ok(Card("WEB-9", title, Priority.Medium, columnId)));
    }

    public Task<Result<CardView>> MoveCardAsync(string workItemKey, long toColumnId, CardPlacement placement,
        byte[] expectedVersion, CancellationToken ct)
    {
        Moves.Add((workItemKey, toColumnId, placement, expectedVersion));
        return Task.FromResult(NextMoveResult?.Invoke()
            ?? Result<CardView>.Ok(Card(workItemKey, "moved", Priority.Medium, toColumnId)));
    }
}
