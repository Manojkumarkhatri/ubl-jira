using System.Diagnostics;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Performance.Tests;

/// <summary>SC-002: with 300 concurrent users on 500,000 work items, the project list, "My tasks", a board of up to
/// 500 visible cards, the List view (sorted and filtered), inline creation, a card move, opening the drawer, saving an
/// edit, assigning, setting dates and a membership change each respond within 1 second at the 95th percentile. Each simulated user signs in as a seeded user and repeats a realistic mix
/// of actions with 1 to 3 seconds of thinking time in a project they belong to (<see cref="LoadDatabase.Subjects"/>):
/// every tenth user works on the largest board, and every tenth (offset by five) is a Project Admin who also changes
/// the team.</summary>
[Trait("Category", "Performance")]
public sealed class Sc002LoadTests(LoadDatabase database) : IClassFixture<LoadDatabase>
{
    private const double TargetMilliseconds = 1_000;

    private static readonly string[] Operations =
    [
        "project list", "My tasks load", "board load", "list load (sorted and filtered)", "inline creation", "card move",
        "drawer open", "saving an edit",
        "assigning", "setting dates", "membership change",
    ];

    [Fact(Timeout = 60 * 60 * 1000)]
    public async Task SC002_Main_actions_respond_within_one_second_at_the_95th_percentile()
    {
        var settings = database.Settings;
        var ct = TestContext.Current.CancellationToken;
        await using var harness = new PerfHarness(database.ConnectionString);
        var recorder = new LatencyRecorder();
        var measureFrom = Stopwatch.GetTimestamp() + (long)(settings.WarmUp.TotalSeconds * Stopwatch.Frequency);
        var stopAt = measureFrom + (long)(settings.Duration.TotalSeconds * Stopwatch.Frequency);

        await Task.WhenAll(Enumerable.Range(0, settings.Users)
            .Select(i => new SimulatedUser(i, database.Subjects[i], database, harness, recorder, measureFrom, stopAt).RunAsync(ct)));

        var stats = recorder.Summaries();
        var report = LatencyRecorder.Report(stats);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"SC-002 load test: {settings.Users} users, {database.WorkItems:N0} work items, {settings.Duration.TotalSeconds:N0} s measured.");
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sc002-report.md"),
            $"# SC-002 load test\n\n{settings.Users} users, {database.WorkItems:N0} work items, " +
            $"{settings.Duration.TotalSeconds:N0} s measured after a {settings.WarmUp.TotalSeconds:N0} s warm-up.\n\n{report}", ct);

        foreach (var operation in Operations)
        {
            var s = stats.SingleOrDefault(x => x.Operation == operation);
            Assert.True(s is { Calls: > 0 }, $"No '{operation}' calls were measured.");
            Assert.True(s.P95 <= TargetMilliseconds, $"'{operation}' p95 is {s.P95:F0} ms (target {TargetMilliseconds:F0} ms).");
            Assert.True(s.Failures <= s.Calls / 100, $"'{operation}' failed {s.Failures} of {s.Calls} times.");
        }
    }

    /// <summary>One person using the app: board, drawer, edits, moves and new tasks, with pauses in between; a Project
    /// Admin also changes the team now and then.</summary>
    private sealed class SimulatedUser(int index, Subject subject, LoadDatabase database, PerfHarness harness,
        LatencyRecorder recorder, long measureFrom, long stopAt)
    {
        private static readonly Priority[] Priorities = Enum.GetValues<Priority>();

        /// <summary>Questions people ask of a list: the newest work, what is overdue, their own, what is in progress…</summary>
        private static readonly WorkItemListQuery[] ListQueries =
        [
            new(),
            new(ListSort.DueDate, false, Due: DueFilter.Overdue),
            new(ListSort.Updated, true, AssignedToMe: true),
            new(ListSort.Priority, false, Categories: [StatusCategory.InProgress]),
            new(ListSort.Assignee, false, Due: DueFilter.Next7Days),
            new(ListSort.Status, false, Unassigned: true),
            new(ListSort.Title, false, Text: "report"),
            new(Page: 3),
        ];

        private readonly Random _random = new(index * 7_919 + 17);
        private readonly Guid _userId = subject.UserId;
        private readonly string _projectKey = subject.ProjectKey;
        private BoardView? _board;
        private WorkItemDetails? _details;
        private TeamView? _team;
        private Guid? _guest;

        public async Task RunAsync(CancellationToken ct)
        {
            await Task.Delay(_random.Next(0, 3_000), ct); // people do not all click at the same moment
            while (Stopwatch.GetTimestamp() < stopAt)
            {
                try
                {
                    await ActAsync(ct);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    recorder.Record("unexpected error", TimeSpan.Zero, Outcome.Failed);
                    _board = null;
                    _details = null;
                    _team = null;
                }

                await Task.Delay(_random.Next(1_000, 3_000), ct);
            }
        }

        private async Task ActAsync(CancellationToken ct)
        {
            var roll = _random.Next(100);
            if (roll < 8)
            {
                await MeasureAsync("project list", () =>
                    harness.CallAsync<IProjectService, Result<Page<ProjectSummary>>>(_userId, s => s.ListAsync(new PageRequest(_random.Next(1, 21)), ct)));
                return;
            }

            if (roll < 13)
            {
                await MeasureAsync("My tasks load", () =>
                    harness.CallAsync<IMyTasksService, Result<Page<MyTaskRow>>>(_userId, s => s.ListAsync(PageRequest.First, ct)));
                return;
            }

            if (roll < 20)
            {
                var query = ListQueries[_random.Next(ListQueries.Length)];
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                await MeasureAsync("list load (sorted and filtered)", () =>
                    harness.CallAsync<IWorkItemListService, Result<WorkItemListView>>(_userId, s => s.ListAsync(_projectKey, query, today, ct)));
                return;
            }

            if (_board is null || roll < 40)
            {
                await LoadBoardAsync(ct);
                return;
            }

            var cards = _board.Columns.SelectMany(c => c.Cards).ToList();
            if (roll < 62 || (roll < 80 && _details is null))
            {
                if (cards.Count > 0)
                {
                    var key = cards[_random.Next(cards.Count)].Key;
                    var opened = await MeasureAsync("drawer open", () =>
                        harness.CallAsync<IWorkItemService, Result<WorkItemDetails>>(_userId, s => s.GetAsync(key, ct)));
                    _details = opened.Value;
                }

                return;
            }

            if (roll < 80)
            {
                var details = _details!;
                var (operation, edit) = roll switch
                {
                    < 70 => ("saving an edit", _random.Next(2) == 0
                        ? new WorkItemEdit.Priority(Priorities[_random.Next(Priorities.Length)])
                        : (WorkItemEdit)new WorkItemEdit.Title($"{details.Title.Split(" (", 2)[0]} ({_random.Next(1_000)})")),
                    < 75 => ("assigning", new WorkItemEdit.Assignee(
                        details.AssigneeOptions.Count == 0 || _random.Next(10) == 0
                            ? null
                            : details.AssigneeOptions[_random.Next(details.AssigneeOptions.Count)].UserId)),
                    _ => ("setting dates", RandomDates()),
                };
                var saved = await MeasureAsync(operation, () =>
                    harness.CallAsync<IWorkItemService, Result<WorkItemDetails>>(_userId, s => s.UpdateAsync(details.Key, edit, details.Version, ct)));
                _details = saved.Value;
                return;
            }

            if (roll < 90)
            {
                if (cards.Count > 0)
                {
                    var card = cards[_random.Next(cards.Count)];
                    var target = _board.Columns[_random.Next(_board.Columns.Count)];
                    await MeasureAsync("card move", () =>
                        harness.CallAsync<IBoardService, Result<CardView>>(_userId, s => s.MoveCardAsync(card.Key, target.Id, CardPlacement.AtEnd, card.Version, ct)));
                    await LoadBoardAsync(ct); // the board refreshes after a move, as in the app
                }

                return;
            }

            if (subject.ChangesTeam && roll >= 95)
            {
                await ChangeTeamAsync(ct);
                return;
            }

            var toDo = _board.Columns.First(c => c.Category == StatusCategory.ToDo).Id;
            await MeasureAsync("inline creation", () =>
                harness.CallAsync<IBoardService, Result<CardView>>(_userId, s => s.CreateInlineAsync(_projectKey, toDo, $"Load test task {_random.Next(100_000)}", ct)));
            await LoadBoardAsync(ct); // and after a new task
        }

        /// <summary>A due date within the next two months, often with a start date up to two weeks before it.</summary>
        private WorkItemEdit.Dates RandomDates()
        {
            var due = new DateOnly(2026, 9, 27).AddDays(_random.Next(0, 60));
            return new WorkItemEdit.Dates(_random.Next(100) < 60 ? due.AddDays(-_random.Next(0, 15)) : null, due);
        }

        /// <summary>Adds someone as a Viewer, and the next time removes them again, so the contributors the other
        /// simulated users act as stay in their teams.</summary>
        private async Task ChangeTeamAsync(CancellationToken ct)
        {
            if (_team is null)
            {
                var opened = await harness.CallAsync<IProjectMemberService, Result<TeamView>>(_userId, s => s.GetTeamAsync(_projectKey, ct));
                _team = opened.Value;
                if (_team is null)
                {
                    return;
                }
            }

            var version = _team.MembersVersion;
            Result<TeamView> changed;
            if (_guest is { } guest)
            {
                changed = await MeasureAsync("membership change", () =>
                    harness.CallAsync<IProjectMemberService, Result<TeamView>>(_userId, s => s.RemoveAsync(_projectKey, guest, version, ct)));
                if (changed.IsSuccess)
                {
                    _guest = null;
                }
            }
            else
            {
                var person = database.UserIds[_random.Next(database.UserIds.Count)];
                if (_team.Members.Any(m => m.UserId == person))
                {
                    return; // the admin would not pick someone who is already in the team
                }

                changed = await MeasureAsync("membership change", () =>
                    harness.CallAsync<IProjectMemberService, Result<TeamView>>(_userId, s => s.AddAsync(_projectKey, person, ProjectRole.Viewer, version, ct)));
                if (changed.IsSuccess)
                {
                    _guest = person;
                }
            }

            _team = changed.Value ?? changed.Error?.Current as TeamView;
        }

        private async Task LoadBoardAsync(CancellationToken ct)
        {
            var loaded = await MeasureAsync("board load", () =>
                harness.CallAsync<IBoardService, Result<BoardView>>(_userId, s => s.GetAsync(_projectKey, false, ct)));
            _board = loaded.Value;
        }

        private async Task<Result<T>> MeasureAsync<T>(string operation, Func<Task<Result<T>>> call)
        {
            var started = Stopwatch.GetTimestamp();
            var result = await call();
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (started >= measureFrom && started < stopAt)
            {
                var outcome = result.IsSuccess ? Outcome.Ok : result.Error!.Kind == ErrorKind.Conflict ? Outcome.Conflict : Outcome.Failed;
                recorder.Record(operation, elapsed, outcome);
            }

            return result;
        }
    }
}
