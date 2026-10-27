using System.Diagnostics;
using System.Text.Json.Nodes;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Notifications;
using Upms.Application.Projects;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Performance.Tests;

/// <summary>SC-002: with 300 concurrent users on 500,000 work items, the project list, "My tasks", a board of up to
/// 500 visible cards, the List view (sorted and filtered), the timeline, inline creation, a card move, opening the
/// drawer, saving an edit, assigning, setting dates, rescheduling on the timeline and a membership change each respond
/// within 1 second at the 95th percentile. Each simulated user signs in as a seeded user and repeats a realistic mix
/// of actions with 1 to 3 seconds of thinking time in a project they belong to (<see cref="LoadDatabase.Subjects"/>):
/// every tenth user works on the largest board, and every tenth (offset by five) is a Project Admin who also changes
/// the team. Phase 4 adds the unread count, the notification panel, comments with a mention, key jumps, word searches
/// (also as an administrator) and saved-filter runs. The Jira parity pass adds the sidebar, opening and starring a space,
/// the Summary page, the four "For you" tabs, the Create form's choices, sub-task cards on boards and lane moves.</summary>
[Trait("Category", "Performance")]
public sealed class Sc002LoadTests(LoadDatabase database) : IClassFixture<LoadDatabase>
{
    private const double TargetMilliseconds = 1_000;

    private static readonly string[] Operations =
    [
        "project list", "My tasks load", "board load", "list load (sorted and filtered)", "timeline load", "inline creation",
        "card move", "drawer open", "saving an edit", "rescheduling",
        "assigning", "setting dates", "membership change",
        "unread count", "notification panel", "comment with a mention",
        "key jump", "word search", "word search (administrator)", "saved filter run",
        "sidebar", "space visit", "star or unstar", "summary load", "For you: worked on", "For you: viewed",
        "For you: assigned to me", "For you: starred", "Create form options", "lane move",
    ];

    /// <summary>What people in Scrum projects also do (Phase 3 research R25).</summary>
    private static readonly string[] ScrumOperations = ["backlog load", "backlog reorder", "sprint move", "sprint report"];

    /// <summary>What people in Stage-gate projects also do; decisions come only from the gates' approvers, so they are
    /// checked whenever some were made (research R25).</summary>
    private static readonly string[] StageGateOperations = ["stages load", "gate request", "dependency change"];

    private static readonly string[] OptionalOperations = ["gate decision"];

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

        var subjects = database.Subjects.Take(settings.Users).ToList();
        IEnumerable<string> required = Operations;
        if (subjects.Any(s => database.ScrumProjects.Contains(s.ProjectKey)))
        {
            required = required.Concat(ScrumOperations);
        }

        if (subjects.Any(s => database.StageGateProjects.Contains(s.ProjectKey)))
        {
            required = required.Concat(StageGateOperations);
        }

        foreach (var operation in required.Concat(OptionalOperations.Where(o => stats.Any(s => s.Operation == o))))
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

        /// <summary>Questions people ask of a list: the newest work, what is overdue, their own, what is in progress, the
        /// open bugs, the work in no epic or stage (Phase 3 research R25)…</summary>
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
            new(ListSort.Priority, false, Categories: [StatusCategory.ToDo, StatusCategory.InProgress], Types: [WorkItemType.Bug]),
            new(ListSort.Type, false, NoContainer: true),
        ];

        /// <summary>What people type in the search box: common and rare words from the seeded titles and comments, and the
        /// start of a word (research R26).</summary>
        private static readonly string[] SearchedWords =
        [
            "reconciliation", "refund settlement", "pipeline", "regression staging", "budget", "vendor invoice", "dashboard churn",
            "migration index", "report", "compliance retention", "INC12", "printer", "accuracy dataset", "onboard",
        ];

        private readonly Random _random = new(index * 7_919 + 17);
        private readonly Guid _userId = subject.UserId;
        private readonly string _projectKey = subject.ProjectKey;
        private BoardView? _board;
        private WorkItemDetails? _details;
        private TeamView? _team;
        private Guid? _guest;
        private List<TimelineItem>? _scheduled;
        private BacklogView? _backlog;
        private StagesView? _stages;
        private List<TimelineItem> _linkable = [];
        private HashSet<(string WaitedFor, string Waiting)> _links = [];
        private (string Waiting, string WaitedFor)? _added;
        private readonly bool _isScrum = database.ScrumProjects.Contains(subject.ProjectKey);
        private readonly bool _isStageGate = database.StageGateProjects.Contains(subject.ProjectKey);

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
                    _scheduled = null;
                }

                await Task.Delay(_random.Next(1_000, 3_000), ct);
            }
        }

        private async Task ActAsync(CancellationToken ct)
        {
            // Phase 4 (research R26): the bell's count, which every page asks for, the panel, and comments with a mention.
            var collaboration = _random.Next(100);
            if (collaboration < 10)
            {
                await MeasureAsync("unread count", async () =>
                    Result<int>.Ok(await harness.CallAsync<INotificationService, int>(_userId, s => s.CountUnreadAsync(ct))));
                return;
            }

            if (collaboration < 13)
            {
                await MeasureAsync("notification panel", () =>
                    harness.CallAsync<INotificationService, Result<Page<NotificationView>>>(_userId, s => s.ListAsync(PageRequest.First, ct)));
                return;
            }

            if (collaboration < 16 && _details is { } discussed)
            {
                var document = MentionDocument(discussed.AssigneeOptions);
                await MeasureAsync("comment with a mention", () =>
                    harness.CallAsync<ICommentService, Result<CommentView>>(_userId, s => s.AddAsync(discussed.Key, document, ct)));
                return;
            }

            // Search (research R26): jumping to a key, words across one's projects (and everything, as an administrator),
            // and running a saved filter.
            if (collaboration < 19 && _details is { } looked)
            {
                await MeasureAsync("key jump", () => harness.CallAsync<ISearchService, Result<string>>(_userId, s => s.ResolveKeyAsync(looked.Key, ct)));
                return;
            }

            if (collaboration < 23)
            {
                var words = SearchedWords[_random.Next(SearchedWords.Length)];
                var (operation, searcher) = _random.Next(10) == 0 ? ("word search (administrator)", database.AdministratorId) : ("word search", _userId);
                await MeasureAsync(operation, () => harness.CallAsync<ISearchService, Result<SearchResults>>(searcher, s =>
                    s.SearchAsync(new SearchQuery(words), PageRequest.First, ct)));
                return;
            }

            if (collaboration < 25)
            {
                await RunSavedFilterAsync(ct);
                return;
            }

            // Jira parity pass (research R20): the sidebar, which every page loads; opening a space, which records the visit;
            // a star; the space's summary; the four "For you" tabs; and the Create form's choices.
            var navigation = _random.Next(100);
            if (navigation < 8)
            {
                await MeasureAsync("sidebar", () =>
                    harness.CallAsync<ISpaceNavigationService, Result<SidebarView>>(_userId, s => s.GetSidebarAsync(ct)));
                return;
            }

            if (navigation < 11)
            {
                await MeasureAsync("space visit", () => Done(harness.CallAsync<ISpaceNavigationService, Result>(_userId, s =>
                    s.RecordVisitAsync(_projectKey, ct))));
                return;
            }

            if (navigation < 12)
            {
                await MeasureAsync("star or unstar", () => Done(harness.CallAsync<ISpaceNavigationService, Result>(_userId, s =>
                    _random.Next(2) == 0 ? s.StarAsync(_projectKey, ct) : s.UnstarAsync(_projectKey, ct))));
                return;
            }

            if (navigation < 15)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                await MeasureAsync("summary load", () =>
                    harness.CallAsync<ISpaceSummaryService, Result<SpaceSummary>>(_userId, s => s.GetAsync(_projectKey, today, ct)));
                return;
            }

            if (navigation < 19)
            {
                await LoadForYouTabAsync(ct);
                return;
            }

            if (navigation < 21)
            {
                await MeasureAsync("Create form options", () =>
                    harness.CallAsync<ICreateWorkService, Result<CreateOptions>>(_userId, s => s.OptionsAsync(_projectKey, ct)));
                return;
            }

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

            if (roll < 25)
            {
                await LoadTimelineAsync(ct);
                return;
            }

            if (_board is null || roll < 40)
            {
                await LoadBoardAsync(ct);
                return;
            }

            if (_isScrum && roll < 50)
            {
                await UseBacklogAsync(roll, ct);
                return;
            }

            if (_isStageGate && roll < 50)
            {
                await UseStagesAsync(roll, ct);
                return;
            }

            if (roll is >= 78 and < 80)
            {
                await RescheduleAsync(ct);
                return;
            }

            var cards = _board.Columns.SelectMany(c => c.Cards).ToList();
            if (roll < 62 || (roll < 78 && _details is null))
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

            if (roll < 78)
            {
                var details = _details!;
                var (operation, edit) = roll switch
                {
                    < 70 => ("saving an edit", _random.Next(2) == 0
                        ? new WorkItemEdit.Priority(Priorities[_random.Next(Priorities.Length)])
                        : (WorkItemEdit)new WorkItemEdit.Title($"{details.Title.Split(" (", 2)[0]} ({_random.Next(1_000)})")),
                    < 74 => ("assigning", new WorkItemEdit.Assignee(
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
                    // One move in five is into another lane, as with the board grouped by epic or person (Jira parity pass
                    // FR-027): to another epic of the board, or to the mover.
                    var epic = cards.Select(c => c.Container).OfType<ContainerView>()
                        .FirstOrDefault(c => c.Type == WorkItemType.Epic && c.Key != card.Container?.Key);
                    var (operation, lane) = _random.Next(5) != 0 ? ("card move", (LaneChange?)null)
                        : card.ParentKey is null && epic is not null && _random.Next(2) == 0 ? ("lane move", new LaneChange.Container(epic.Key))
                        : ("lane move", new LaneChange.Assignee(_userId));
                    await MeasureAsync(operation, () =>
                        harness.CallAsync<IBoardService, Result<CardView>>(_userId, s => s.MoveCardAsync(card.Key, target.Id, CardPlacement.AtEnd, card.Version, lane, ct)));
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
                harness.CallAsync<IBoardService, Result<CardView>>(_userId, s => s.CreateInlineAsync(_projectKey, toDo, $"Load test task {_random.Next(100_000)}", Upms.Domain.Work.WorkItemType.Task, ct)));
            await LoadBoardAsync(ct); // and after a new task
        }

        /// <summary>Opens one of the person's saved filters, their own or shared with them, and runs its search.</summary>
        private async Task RunSavedFilterAsync(CancellationToken ct)
        {
            var filters = await harness.CallAsync<ISavedFilterService, Result<SavedFilterList>>(_userId, s => s.ListAsync(ct));
            var available = filters.IsSuccess ? filters.Value!.Mine.Concat(filters.Value.SharedWithMe).ToList() : [];
            if (available.Count == 0)
            {
                return;
            }

            var chosen = available[_random.Next(available.Count)];
            await MeasureAsync("saved filter run", async () =>
            {
                var filter = await harness.CallAsync<ISavedFilterService, Result<SavedFilterView>>(_userId, s => s.GetAsync(chosen.Id, ct));
                return filter.IsSuccess
                    ? await harness.CallAsync<ISearchService, Result<SearchResults>>(_userId, s =>
                        s.SearchAsync(SavedQuery.Parse(filter.Value!.Query), PageRequest.First, ct))
                    : Result<SearchResults>.Fail(filter.Error!);
            });
        }

        /// <summary>A short comment that mentions one of the people who can be assigned, as the editor sends it.</summary>
        private string MentionDocument(IReadOnlyList<AssigneeOption> people)
        {
            var inline = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "Load test comment for " });
            if (people.Count > 0)
            {
                var person = people[_random.Next(people.Count)];
                inline.Add(new JsonObject
                {
                    ["type"] = "mention",
                    ["attrs"] = new JsonObject { ["id"] = person.UserId.ToString(), ["label"] = person.DisplayName },
                });
            }

            return new JsonObject
            {
                ["type"] = "doc",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = inline }),
            }.ToJsonString();
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

        /// <summary>Opens the timeline, now and then without completed tasks, and remembers its bars (with epics, stages,
        /// milestones and their items since Phase 3), its top-level items and the links between them.</summary>
        private async Task LoadTimelineAsync(CancellationToken ct)
        {
            var loaded = await MeasureAsync("timeline load", () =>
                harness.CallAsync<ITimelineService, Result<TimelineView>>(_userId, s => s.GetAsync(_projectKey, _random.Next(3) == 0, ct)));
            if (loaded.Value is not { } view)
            {
                _scheduled = null;
                return;
            }

            var rows = view.Groups.SelectMany(g => g.Rows).Concat(view.Rows).ToList();
            _scheduled = view.Groups.Select(g => g.Container).Concat(rows.SelectMany(r => r.ScheduledSubtasks.Prepend(r.Task)))
                .Where(i => i.IsScheduled).ToList();
            _linkable = rows.Select(r => r.Task).ToList();
            _links = view.Dependencies.Select(d => (d.WaitedForKey, d.WaitingKey)).ToHashSet();
        }

        /// <summary>Adds a dependency between two items of the timeline, then removes it the next time. A later item always
        /// waits for an earlier one, as in the seed, so no link makes a loop.</summary>
        private async Task ChangeDependencyAsync(CancellationToken ct)
        {
            if (_added is { } added)
            {
                _added = null;
                await MeasureAsync("dependency change", () => harness.CallAsync<IDependencyService, Result<DependencyList>>(_userId, s =>
                    s.RemoveAsync(added.Waiting, added.WaitedFor, ct)));
                return;
            }

            if (_linkable.Count < 2)
            {
                await LoadTimelineAsync(ct);
                return;
            }

            var (a, b) = (_linkable[_random.Next(_linkable.Count)], _linkable[_random.Next(_linkable.Count)]);
            var (waitedFor, waiting) = NumberOf(a.Key) < NumberOf(b.Key) ? (a.Key, b.Key) : (b.Key, a.Key);
            if (waitedFor == waiting || _links.Contains((waitedFor, waiting)))
            {
                return;
            }

            // The timeline shows only links whose two items have dates, and another user may add the same link meanwhile:
            // "already exists" is then the right answer.
            var result = await MeasureAsync("dependency change", () => harness.CallAsync<IDependencyService, Result<DependencyList>>(_userId, s =>
                s.AddAsync(waiting, waitedFor, ct)), DependencyGraph.DuplicateCode);
            if (result.IsSuccess)
            {
                _added = (waiting, waitedFor);
            }
        }

        private static int NumberOf(string key) => int.Parse(key.AsSpan(key.LastIndexOf('-') + 1), System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Moves a bar up to two weeks either way, as a drag or the arrow keys would.</summary>
        private async Task RescheduleAsync(CancellationToken ct)
        {
            if (_scheduled is not { Count: > 0 })
            {
                await LoadTimelineAsync(ct);
                return;
            }

            var index = _random.Next(_scheduled.Count);
            var item = _scheduled[index];
            var days = _random.Next(1, 15) * (_random.Next(2) == 0 ? -1 : 1);
            var moved = await MeasureAsync("rescheduling", () =>
                harness.CallAsync<ITimelineService, Result<TimelineItem>>(_userId, s =>
                    s.RescheduleAsync(item.Key, item.StartDate?.AddDays(days), item.DueDate?.AddDays(days), item.Version, ct)));
            _scheduled[index] = moved.Value ?? moved.Error?.Current as TimelineItem ?? item;
        }

        /// <summary>The backlog: open it, reorder it, move cards between it and the planned sprint, or read a past
        /// sprint's report. Cards move in and out of the planned sprint only, so the active sprint keeps its size.</summary>
        private async Task UseBacklogAsync(int roll, CancellationToken ct)
        {
            if (_backlog is null || roll < 43)
            {
                await LoadBacklogAsync(ct);
                return;
            }

            var rows = _backlog.Backlog.Items;
            if (roll < 46)
            {
                if (rows.Count > 1)
                {
                    var row = rows[_random.Next(rows.Count)];
                    BacklogPlacement placement = _random.Next(4) switch
                    {
                        0 => BacklogPlacement.AtTop,
                        1 => BacklogPlacement.OneUp,
                        2 => BacklogPlacement.OneDown,
                        _ => new BacklogPlacement.Before(rows[_random.Next(rows.Count)].Key),
                    };
                    await MeasureAsync("backlog reorder", () => harness.CallAsync<IBacklogService, Result<BacklogRow>>(_userId, s =>
                        s.MoveAsync(row.Key, BacklogTarget.ToBacklog, placement, row.Version, ct)));
                    await LoadBacklogAsync(ct); // the backlog refreshes after a move, as in the app
                }

                return;
            }

            if (roll < 49)
            {
                if (_backlog.Planned.Count == 0)
                {
                    return;
                }

                var planned = _backlog.Planned[0];
                var (row, target) = planned.Items.Count > 0 && (_random.Next(2) == 0 || rows.Count == 0)
                    ? (planned.Items[_random.Next(planned.Items.Count)], BacklogTarget.ToBacklog)
                    : rows.Count > 0 ? (rows[_random.Next(rows.Count)], new BacklogTarget.Sprint(planned.Id)) : default;
                if (row is not null)
                {
                    await MeasureAsync("sprint move", () => harness.CallAsync<IBacklogService, Result<BacklogRow>>(_userId, s =>
                        s.MoveAsync(row.Key, target, BacklogPlacement.KeepRank, row.Version, ct)));
                    await LoadBacklogAsync(ct);
                }

                return;
            }

            if (_backlog.Completed.Count > 0)
            {
                var number = _backlog.Completed[_random.Next(_backlog.Completed.Count)].Number;
                await MeasureAsync("sprint report", () =>
                    harness.CallAsync<ISprintService, Result<SprintReport>>(_userId, s => s.GetReportAsync(_projectKey, number, ct)));
            }
        }

        /// <summary>The Stages view, dependencies and the current stage's gate: contributors add and remove links, and
        /// request approval or withdraw the request, and approvers send it back with a note, so the gate keeps cycling
        /// instead of opening the locked stage.</summary>
        private async Task UseStagesAsync(int roll, CancellationToken ct)
        {
            if (_stages is null || roll < 43)
            {
                await LoadStagesAsync(ct);
                return;
            }

            if (roll < 46)
            {
                await ChangeDependencyAsync(ct);
                return;
            }

            if (_stages.Stages.FirstOrDefault(s => s.Place == StagePlace.Current) is not { } current)
            {
                return;
            }

            var gate = current.Gate;
            if (gate.State == GateState.AwaitingDecision && gate.CanDecide && gate.CurrentRequest is { } pending)
            {
                await MeasureAsync("gate decision", () => harness.CallAsync<IStageGateService, Result<StageView>>(_userId, s =>
                    s.DecideAsync(current.Key, pending.Number, GateDecisionKind.SendBack, "More detail is needed.", ct)));
            }
            else if (gate.State is GateState.NotRequested or GateState.SentBack)
            {
                await MeasureAsync("gate request", () => harness.CallAsync<IStageGateService, Result<StageView>>(_userId, s =>
                    s.RequestApprovalAsync(current.Key, gate.Version, ct)));
            }
            else if (gate.State == GateState.AwaitingDecision && gate.CurrentRequest is { } request)
            {
                await MeasureAsync("gate request", () => harness.CallAsync<IStageGateService, Result<StageView>>(_userId, s =>
                    s.WithdrawAsync(current.Key, request.Number, ct)));
            }

            await LoadStagesAsync(ct); // the view refreshes after a change, as in the app
        }

        private async Task LoadStagesAsync(CancellationToken ct)
        {
            var loaded = await MeasureAsync("stages load", () =>
                harness.CallAsync<IStageGateService, Result<StagesView>>(_userId, s => s.GetAsync(_projectKey, ct)));
            _stages = loaded.Value;
        }

        private async Task LoadBacklogAsync(CancellationToken ct)
        {
            var loaded = await MeasureAsync("backlog load", () =>
                harness.CallAsync<IBacklogService, Result<BacklogView>>(_userId, s => s.GetAsync(_projectKey, PageRequest.First, ct)));
            _backlog = loaded.Value;
        }

        /// <summary>One of "For you"'s tabs: Worked on, Viewed, Assigned to me (the old My tasks) and Starred.</summary>
        private Task LoadForYouTabAsync(CancellationToken ct) => _random.Next(4) switch
        {
            0 => MeasureAsync("For you: worked on", () =>
                harness.CallAsync<ISearchService, Result<Page<SearchResultRow>>>(_userId, s => s.WorkedOnAsync(PageRequest.First, ct))),
            1 => MeasureAsync("For you: viewed", () =>
                harness.CallAsync<ISearchService, Result<IReadOnlyList<SearchResultRow>>>(_userId, s => s.RecentAsync(ct))),
            2 => MeasureAsync("For you: assigned to me", () =>
                harness.CallAsync<IMyTasksService, Result<Page<MyTaskRow>>>(_userId, s => s.ListAsync(PageRequest.First, ct))),
            _ => MeasureAsync("For you: starred", () =>
                harness.CallAsync<ISpaceNavigationService, Result<IReadOnlyList<SpaceLink>>>(_userId, s => s.StarredAsync(ct))),
        };

        /// <summary>An action that returns no value, measured like the others.</summary>
        private static async Task<Result<bool>> Done(Task<Result> call) =>
            await call is { IsSuccess: false, Error: { } error } ? Result<bool>.Fail(error) : Result<bool>.Ok(true);

        private async Task LoadBoardAsync(CancellationToken ct)
        {
            var loaded = await MeasureAsync("board load", () =>
                harness.CallAsync<IBoardService, Result<BoardView>>(_userId, s => s.GetAsync(_projectKey, false, ct)));
            _board = loaded.Value;
        }

        /// <param name="raceCode">A refusal that only means someone else got there first, counted with the conflicts.</param>
        private async Task<Result<T>> MeasureAsync<T>(string operation, Func<Task<Result<T>>> call, string? raceCode = null)
        {
            var started = Stopwatch.GetTimestamp();
            var result = await call();
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (started >= measureFrom && started < stopAt)
            {
                var outcome = result.IsSuccess ? Outcome.Ok
                    : result.Error!.Kind == ErrorKind.Conflict || result.Error.Code == raceCode ? Outcome.Conflict
                    : Outcome.Failed;
                recorder.Record(operation, elapsed, outcome);
            }

            return result;
        }
    }
}
