using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>An in-memory Scrum backlog for component tests: MOB with an active sprint, a planned sprint and three backlog
/// cards. Moves are recorded and applied, so the page reads the new order back.</summary>
public sealed class FakeBacklogService : IBacklogService
{
    public const long ActiveId = 11;
    public const long PlannedId = 12;
    public static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    public static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);
    public static readonly StatusOption Done = new(4, "Done", StatusCategory.Done);

    public BacklogView View { get; set; } = Sample();

    /// <summary>The backlog's second page, for "Show more".</summary>
    public List<BacklogRow> SecondPage { get; set; } = [];

    public List<int> PagesRead { get; } = [];

    public List<(string Key, BacklogTarget Target, BacklogPlacement Placement, byte[] Version)> Moves { get; } = [];

    public Func<Result<BacklogRow>>? NextMoveResult { get; set; }

    public static BacklogRow Row(string key, WorkItemType type, string title, decimal? estimate, StatusOption? status = null,
        ContainerView? epic = null, AssigneeRef? assignee = null) =>
        new(key, type, title, epic, assignee, estimate, status ?? ToDo, 0, [1, 2, 3]);

    public static SprintSection Section(long id, int number, SprintState state, params BacklogRow[] rows) =>
        new(id, number, $"MOB Sprint {number}", state == SprintState.Active ? "Ship card refunds" : null, state,
            state == SprintState.Active ? new DateOnly(2026, 9, 21) : null, state == SprintState.Active ? new DateOnly(2026, 10, 5) : null,
            SprintTotals.Of(rows), rows, [9, 9, (byte)id]);

    public static BacklogView Sample() => new("MOB", "Mobile App", CanContribute: true, CanManageSprints: true, CanRestoreDeleted: false,
        Section(ActiveId, 1, SprintState.Active,
            Row("MOB-2", WorkItemType.Story, "Refund to card", 3, epic: new ContainerView("MOB-1", "Card refunds", WorkItemType.Epic)),
            Row("MOB-3", WorkItemType.Bug, "Refund shows twice", 2, Done)),
        [Section(PlannedId, 2, SprintState.Planned, Row("MOB-4", WorkItemType.Task, "Refund email", null))],
        new Page<BacklogRow>(
        [
            Row("MOB-5", WorkItemType.Story, "Refund by bank transfer", 5),
            Row("MOB-6", WorkItemType.Story, "Refund history", 8),
            Row("MOB-7", WorkItemType.Bug, "Refund total rounds wrong", null),
        ], 3, 1, 100),
        [],
        [ToDo, InProgress, Done]);

    public Task<Result<BacklogView>> GetAsync(string projectKey, PageRequest backlogPage, CancellationToken ct)
    {
        PagesRead.Add(backlogPage.Page);
        if (projectKey != View.ProjectKey)
        {
            return Task.FromResult(Result<BacklogView>.Fail(AppError.NotFound("backlog")));
        }

        return Task.FromResult(Result<BacklogView>.Ok(backlogPage.Page == 1
            ? View
            : View with { Backlog = new Page<BacklogRow>(SecondPage, View.Backlog.TotalCount, backlogPage.Page, 100) }));
    }

    public Task<Result<BacklogRow>> MoveAsync(string workItemKey, BacklogTarget target, BacklogPlacement placement,
        byte[] expectedVersion, CancellationToken ct)
    {
        Moves.Add((workItemKey, target, placement, expectedVersion));
        if (NextMoveResult is { } next)
        {
            NextMoveResult = null;
            return Task.FromResult(next());
        }

        var row = AllRows().Single(r => r.Key == workItemKey);
        var lists = Lists();
        var from = lists.Single(l => l.Rows.Any(r => r.Key == workItemKey));
        var to = target is BacklogTarget.Sprint sprint ? lists.Single(l => l.SprintId == sprint.SprintId) : lists.Single(l => l.SprintId is null);
        var oldIndex = from.Rows.FindIndex(r => r.Key == workItemKey);
        from.Rows.RemoveAt(oldIndex);
        var index = placement switch
        {
            BacklogPlacement.Top => 0,
            BacklogPlacement.Bottom => to.Rows.Count,
            BacklogPlacement.Up => Math.Max(0, oldIndex - 1),
            BacklogPlacement.Down => Math.Min(to.Rows.Count, oldIndex + 1),
            BacklogPlacement.Before before => Math.Max(0, to.Rows.FindIndex(r => r.Key == before.WorkItemKey)),
            _ => from == to ? oldIndex : to.Rows.Count,
        };
        to.Rows.Insert(index, row);
        View = View with
        {
            Active = View.Active is { } active ? active with { Items = lists.Single(l => l.SprintId == active.Id).Rows } : null,
            Planned = View.Planned.Select(p => p with { Items = lists.Single(l => l.SprintId == p.Id).Rows }).ToList(),
            Backlog = View.Backlog with { Items = lists.Single(l => l.SprintId is null).Rows },
        };
        return Task.FromResult(Result<BacklogRow>.Ok(row));
    }

    private IEnumerable<BacklogRow> AllRows() =>
        new[] { View.Active }.OfType<SprintSection>().Concat(View.Planned).SelectMany(s => s.Items).Concat(View.Backlog.Items);

    private List<(long? SprintId, List<BacklogRow> Rows)> Lists() =>
        new[] { View.Active }.OfType<SprintSection>().Concat(View.Planned)
            .Select(s => ((long?)s.Id, s.Items.ToList()))
            .Append((null, View.Backlog.Items.ToList()))
            .ToList();
}
