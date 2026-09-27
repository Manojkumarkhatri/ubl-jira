using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;

namespace Upms.Web.Tests.Fakes;

/// <summary>An in-memory WEB board with To Do (2 items), In Progress (1 item) and an empty Done; records calls.</summary>
public sealed class FakeBoardColumnService : IBoardColumnService
{
    private long _nextId = 10;

    public List<BoardColumnView> Columns { get; } =
    [
        new(1, "To Do", StatusCategory.ToDo, 0, null, 2, false),
        new(2, "In Progress", StatusCategory.InProgress, 1, 3, 1, false),
        new(3, "Done", StatusCategory.Done, 2, null, 0, true),
    ];

    public int Version { get; set; } = 7;

    public List<string> Calls { get; } = [];

    /// <summary>Returned instead of applying the next change.</summary>
    public Func<Result<BoardColumnsView>>? NextResult { get; set; }

    public BoardColumnsView View() =>
        new("WEB", "Website Revamp", Version, Columns.Select((c, i) => c with { Position = i }).ToList());

    public Task<Result<BoardColumnsView>> GetAsync(string projectKey, CancellationToken ct) => Task.FromResult(Result<BoardColumnsView>.Ok(View()));

    public Task<Result<BoardColumnsView>> AddAsync(string projectKey, string name, StatusCategory category, int position,
        int expectedBoardVersion, CancellationToken ct) =>
        Apply($"add {name} {category} {position} v{expectedBoardVersion}",
            () => Columns.Insert(Math.Clamp(position, 0, Columns.Count), new BoardColumnView(_nextId++, name, category, 0, null, 0, true)));

    public Task<Result<BoardColumnsView>> RenameAsync(string projectKey, long columnId, string name, int expectedBoardVersion,
        CancellationToken ct) =>
        Apply($"rename {columnId} {name} v{expectedBoardVersion}", () => Replace(columnId, c => c with { Name = name }));

    public Task<Result<BoardColumnsView>> MoveAsync(string projectKey, long columnId, int newPosition, int expectedBoardVersion,
        CancellationToken ct) =>
        Apply($"move {columnId} {newPosition} v{expectedBoardVersion}", () =>
        {
            var column = Columns.Single(c => c.Id == columnId);
            Columns.Remove(column);
            Columns.Insert(Math.Clamp(newPosition, 0, Columns.Count), column);
        });

    public Task<Result<BoardColumnsView>> SetWipLimitAsync(string projectKey, long columnId, int? limit, int expectedBoardVersion,
        CancellationToken ct) =>
        Apply($"limit {columnId} {limit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"} v{expectedBoardVersion}",
            () => Replace(columnId, c => c with { WipLimit = limit }));

    public Task<Result<BoardColumnsView>> ChangeCategoryAsync(string projectKey, long columnId, StatusCategory category,
        int expectedBoardVersion, CancellationToken ct) =>
        Apply($"type {columnId} {category} v{expectedBoardVersion}", () => Replace(columnId, c => c with { Category = category }));

    public Task<Result<BoardColumnsView>> DeleteAsync(string projectKey, long columnId, long? destinationColumnId,
        int expectedBoardVersion, CancellationToken ct) =>
        Apply($"delete {columnId} to {destinationColumnId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"} v{expectedBoardVersion}",
            () => Columns.RemoveAll(c => c.Id == columnId));

    private Task<Result<BoardColumnsView>> Apply(string call, Action change)
    {
        Calls.Add(call);
        if (NextResult is { } next)
        {
            NextResult = null;
            return Task.FromResult(next());
        }

        change();
        Version++;
        return Task.FromResult(Result<BoardColumnsView>.Ok(View()));
    }

    private void Replace(long columnId, Func<BoardColumnView, BoardColumnView> change)
    {
        var index = Columns.FindIndex(c => c.Id == columnId);
        Columns[index] = change(Columns[index]);
    }
}
