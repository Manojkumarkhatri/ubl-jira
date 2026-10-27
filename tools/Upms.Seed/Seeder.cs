using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using Upms.Infrastructure.Persistence;

namespace Upms.Seed;

/// <summary>Fills an empty database with realistic volumes (tasks.md T105): users, projects and their teams through
/// the domain and EF Core; work items, their history and comments with bulk inserts. Every project has its owner as
/// Project Admin, 4 to 20 Members and 0 to 3 Viewers (Phase 2 research R15), and only its contributors create,
/// change and comment on its work. About 80% of work items are tasks and 20% sub-tasks; about 25% of tasks are to
/// do, 15% in progress and 60% done (3% of those in the last 14 days), and 1% are deleted. About 70% of work items
/// are assigned to a contributor and about half have a due date, most of those with a start date too. Every
/// generated change has a matching history row. Since Phase 3 (research R25) a third of the projects are Scrum, with
/// epics, stories, tasks and bugs, estimates and one order for the whole project, and a fifth are Stage-gate, with
/// their four stages (two approved, one current and one locked, with gates in every state), most tasks placed in an
/// open stage, milestones and about 50 dependencies. The largest project stays Kanban. Since Phase 4 (research R26)
/// cards have five comments on average, items have watchers, notifications and a few small checked attachments, every
/// item's search words are written, about 30% of items carry some of 200 shared labels, about a third of the people
/// have saved filters, some shared with a project, and people have each email choice, with the last day's emails sent.</summary>
public sealed class Seeder(string connectionString, Action<string> log)
{
    private const int BatchRows = 20_000;

    /// <summary>Members of the largest project: enough for every tenth simulated user of the SC-002 run to work on it
    /// as a different person.</summary>
    private const int LargestProjectMembers = 40;

    private readonly Dictionary<Guid, string> _names = [];

    public async Task<SeedSummary> RunAsync(SeedOptions options, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Users, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Projects, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.WorkItems, options.Projects);
        var clock = Stopwatch.StartNew();
        var random = new Random(options.RandomSeed);
        var now = DateTimeOffset.UtcNow;
        await using (var db = AppDbContext.Create(connectionString))
        {
            if (await db.Projects.AnyAsync(ct))
            {
                throw new InvalidOperationException("The database already has projects. Seed an empty database.");
            }
        }

        var users = await SeedUsersAsync(options, random, now, ct);
        var projects = await SeedProjectsAsync(options, users, random, now, ct);
        var work = new WorkWriter(connectionString, _names, random, now) { Labels = await SeedLabelsAsync(ct) };
        await work.InitializeAsync(ct);
        foreach (var project in projects)
        {
            work.AddProject(project);
            if (work.PendingRows >= BatchRows)
            {
                await work.FlushAsync(ct);
                log($"  {work.WorkItems:N0} work items so far");
            }
        }

        await work.FlushAsync(ct);
        var filters = await SeedSavedFiltersAsync(users, projects, random, now, ct);
        log($"{filters:N0} saved filters");
        var (stars, visits) = await SeedStarsAndVisitsAsync(projects, random, now, ct);
        log($"{stars:N0} stars and {visits:N0} space visits");
        await SeedEmailsAsync(ct);
        await FinishAsync(ct);
        log($"Seeded {users.Count:N0} users, {projects.Count:N0} projects, {work.WorkItems:N0} work items, " +
            $"{work.Changes:N0} history rows and {work.Comments:N0} comments in {clock.Elapsed:mm\\:ss}.");
        return new SeedSummary(users, projects.ConvertAll(p => p.Key), projects[0].Key, work.WorkItems, work.Changes,
            work.Comments, clock.Elapsed);
    }

    private async Task<List<Guid>> SeedUsersAsync(SeedOptions options, Random random, DateTimeOffset now, CancellationToken ct)
    {
        var hash = new PasswordHasher<User>().HashPassword(new User(), SeedOptions.Password);
        var ids = new List<Guid>(options.Users);
        await using var db = AppDbContext.Create(connectionString);
        for (var i = 0; i < options.Users; i++)
        {
            var userName = $"seed.user{i + 1:0000}";
            var user = new User
            {
                Id = NewGuid(random),
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@example.com",
                NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
                EmailConfirmed = true,
                PasswordHash = hash,
                SecurityStamp = NewGuid(random).ToString("N"),
                ConcurrencyStamp = NewGuid(random).ToString(),
                LockoutEnabled = true,
                DisplayName = SampleText.DisplayName(random),
                OrganizationRole = i == 0 ? OrganizationRole.Administrator : OrganizationRole.User,
                // One in ten emails nothing and three in twenty everything; the rest keep the default (Phase 4 FR-051).
                EmailNotifications = (i % 20) switch
                {
                    0 or 1 => EmailChoice.None,
                    2 or 3 or 4 => EmailChoice.All,
                    _ => EmailChoice.AimedAtMe,
                },
                CreatedAt = now.AddDays(-random.Next(30, 400)),
            };
            db.Users.Add(user);
            ids.Add(user.Id);
            _names[user.Id] = user.DisplayName;
            if ((i + 1) % 1_000 == 0)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(ct);
        log($"{ids.Count:N0} users (password \"{SeedOptions.Password}\"; seed.user0001 is an Administrator)");
        return ids;
    }

    private async Task<List<SeededProject>> SeedProjectsAsync(SeedOptions options, List<Guid> users, Random random,
        DateTimeOffset now, CancellationToken ct)
    {
        // The first project is the largest board; the others share the remaining tasks with weights 1 to 5.
        var tasksTotal = options.WorkItems * 4 / 5;
        var subtaskRatio = (options.WorkItems - tasksTotal) / (double)tasksTotal;
        var largest = Math.Min(options.LargestProjectTasks, tasksTotal);
        var weights = Enumerable.Range(1, options.Projects - 1).Select(i => 1 + (i % 5)).ToList();
        var weightSum = Math.Max(1, weights.Sum());
        var tasks = new List<int> { largest };
        tasks.AddRange(weights.Select(w => Math.Max(1, (tasksTotal - largest) * w / weightSum)));

        var created = new List<(Project Project, int Tasks)>();
        var memberships = 0;
        await using (var db = AppDbContext.Create(connectionString))
        {
            for (var i = 0; i < options.Projects; i++)
            {
                var key = i == 0 ? "BIG" : $"P{i:0000}";
                var name = i == 0 ? "Largest Board" : SampleText.ProjectName(i);
                var createdAt = now.AddDays(-random.Next(60, 500));
                var project = Project.Create(name, key, i % 3 == 0 ? $"Seeded project {key}." : null,
                    users[random.Next(users.Count)], createdAt, TemplateOf(i)).Value!;
                AddTeam(project, users, random, i == 0 ? LargestProjectMembers : random.Next(4, 21), random.Next(0, 4), createdAt);
                memberships += project.Members.Count;
                if (i % 5 == 0 && project.Template == ProjectTemplate.Kanban)
                {
                    project.AddColumn("In Review", StatusCategory.InProgress, 2, now);
                }

                db.Projects.Add(project);
                created.Add((project, tasks[i]));
                if ((i + 1) % 200 == 0)
                {
                    await db.SaveChangesAsync(ct);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        log($"{created.Count:N0} projects with {memberships:N0} memberships");
        return created.ConvertAll(c => new SeededProject(c.Project.Id, c.Project.Key, c.Project.Template, c.Tasks,
            (int)Math.Round(c.Tasks * subtaskRatio),
            c.Project.Statuses.Select(s => new SeededStatus(s.Id, s.Name, s.Category)).ToList(),
            c.Project.Members.Where(m => m.Role != ProjectRole.Viewer).Select(m => m.UserId).ToList()));
    }

    /// <summary>Of every 15 projects, 5 are Scrum and 3 Stage-gate; the largest project stays Kanban.</summary>
    private static ProjectTemplate TemplateOf(int index) => index == 0
        ? ProjectTemplate.Kanban
        : (index % 15) switch
        {
            < 5 => ProjectTemplate.Scrum,
            < 8 => ProjectTemplate.StageGate,
            _ => ProjectTemplate.Kanban,
        };

    /// <summary>Adds Members, then Viewers, chosen at random from everyone but the owner (already Project Admin).</summary>
    private static void AddTeam(Project project, List<Guid> users, Random random, int members, int viewers, DateTimeOffset at)
    {
        var taken = new HashSet<Guid> { project.OwnerId };
        var size = Math.Min(members + viewers, users.Count - 1);
        for (var n = 0; n < size; n++)
        {
            Guid userId;
            do
            {
                userId = users[random.Next(users.Count)];
            }
            while (!taken.Add(userId));

            var role = n < members ? ProjectRole.Member : ProjectRole.Viewer;
            if (project.AddMember(userId, role, project.OwnerId, at.AddMinutes(n + 1)).Error is { } error)
            {
                throw new InvalidOperationException(error.Message);
            }
        }
    }

    private async Task FinishAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = """
            UPDATE p SET NextItemNumber = x.MaxNumber + 1
            FROM Projects p JOIN (SELECT ProjectId, MAX(Number) AS MaxNumber FROM WorkItems GROUP BY ProjectId) x ON x.ProjectId = p.Id;
            UPDATE STATISTICS WorkItems WITH FULLSCAN;
            UPDATE STATISTICS WorkItemChanges;
            UPDATE STATISTICS Comments;
            UPDATE STATISTICS Sprints;
            UPDATE STATISTICS SprintScopeEvents;
            UPDATE STATISTICS StageGates;
            UPDATE STATISTICS GateRequests;
            UPDATE STATISTICS Dependencies;
            UPDATE STATISTICS WorkItemWatchers;
            UPDATE STATISTICS Attachments;
            UPDATE STATISTICS Notifications;
            INSERT INTO SearchIndexProgress (Id, LastWorkItemId) SELECT 1, ISNULL(MAX(Id), 0) FROM WorkItems;
            UPDATE STATISTICS SearchTerms;
            UPDATE STATISTICS Labels;
            UPDATE STATISTICS WorkItemLabels;
            UPDATE STATISTICS SavedFilters;
            UPDATE STATISTICS SavedFilterShares;
            UPDATE STATISTICS StarredProjects;
            UPDATE STATISTICS ProjectVisits;
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>The email outbox as a running system leaves it (Phase 4 research R10): the last day's assignments and mentions
    /// sent to people who get them, a few dozen still waiting.</summary>
    private async Task SeedEmailsAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = """
            INSERT INTO EmailMessages (NotificationId, RecipientId, CreatedAt, Attempts, NextAttemptAt, SentAt)
            SELECT n.Id, n.RecipientId, n.OccurredAt, 1, n.OccurredAt, DATEADD(SECOND, 20, n.OccurredAt)
            FROM Notifications n JOIN AspNetUsers u ON u.Id = n.RecipientId
            WHERE n.Kind IN ('Assigned', 'Mentioned') AND u.EmailNotifications <> 'None' AND n.OccurredAt >= DATEADD(DAY, -1, SYSDATETIMEOFFSET());
            UPDATE TOP (40) EmailMessages SET SentAt = NULL, Attempts = 0;
            UPDATE STATISTICS EmailMessages;
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>The 200 shared labels (Phase 4 research R26).</summary>
    private async Task<IReadOnlyList<(int Id, string Name)>> SeedLabelsAsync(CancellationToken ct)
    {
        await using var db = AppDbContext.Create(connectionString);
        var labels = SampleText.LabelNames().Select(name => Label.Create(name).Value!).ToList();
        db.Labels.AddRange(labels);
        await db.SaveChangesAsync(ct);
        log($"{labels.Count:N0} labels");
        return labels.ConvertAll(l => (l.Id, l.Name));
    }

    /// <summary>About a third of the people have one to three saved filters; one in five is shared with a project its
    /// owner works on (Phase 4 research R26).</summary>
    private async Task<int> SeedSavedFiltersAsync(List<Guid> users, List<SeededProject> projects, Random random, DateTimeOffset now,
        CancellationToken ct)
    {
        var projectsOf = ProjectsOf(projects);
        var count = 0;
        await using var db = AppDbContext.Create(connectionString);
        foreach (var userId in users.Where(_ => random.Next(100) < 35))
        {
            var first = random.Next(1_000);
            for (var f = random.Next(1, 4); f > 0; f--)
            {
                var (name, query) = SampleText.Filter(first + f);
                var filter = SavedFilter.Create(userId, name, query, now.AddDays(-random.Next(1, 90))).Value!;
                if (random.Next(100) < 20 && projectsOf.TryGetValue(userId, out var theirs))
                {
                    filter.ShareWith([theirs[random.Next(theirs.Count)]], now);
                }

                db.SavedFilters.Add(filter);
                count++;
            }
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>Each person has opened up to the kept number of the spaces they work on, the last a few minutes to a month
    /// ago, and starred one to three of them; one in five starred none (Jira parity pass research R20).</summary>
    private async Task<(int Stars, int Visits)> SeedStarsAndVisitsAsync(List<SeededProject> projects, Random random, DateTimeOffset now,
        CancellationToken ct)
    {
        var (stars, visits) = (0, 0);
        await using var db = AppDbContext.Create(connectionString);
        foreach (var (userId, theirs) in ProjectsOf(projects))
        {
            var opened = theirs.OrderBy(_ => random.Next()).Take(ProjectVisit.Kept).ToList();
            foreach (var projectId in opened)
            {
                db.ProjectVisits.Add(ProjectVisit.Of(userId, projectId, now.AddMinutes(-random.Next(5, 30 * 24 * 60))));
                visits++;
            }

            if (random.Next(100) >= 20)
            {
                foreach (var projectId in opened.Take(random.Next(1, 4)))
                {
                    db.StarredProjects.Add(StarredProject.Of(userId, projectId, now.AddDays(-random.Next(1, 60))));
                    stars++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return (stars, visits);
    }

    /// <summary>The spaces each person works on (as Space admin or Member).</summary>
    private static Dictionary<Guid, List<long>> ProjectsOf(List<SeededProject> projects)
    {
        var projectsOf = new Dictionary<Guid, List<long>>();
        foreach (var project in projects)
        {
            foreach (var userId in project.Contributors)
            {
                if (!projectsOf.TryGetValue(userId, out var theirs))
                {
                    projectsOf[userId] = theirs = [];
                }

                theirs.Add(project.Id);
            }
        }

        return projectsOf;
    }

    internal static Guid NewGuid(Random random)
    {
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // version 4
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes);
    }

    private sealed record SeededStatus(long Id, string Name, StatusCategory Category);

    /// <param name="Contributors">The Project Admin and Members, who create, change and comment on the work.</param>
    private sealed record SeededProject(long Id, string Key, ProjectTemplate Template, int Tasks, int Subtasks,
        List<SeededStatus> Statuses, List<Guid> Contributors);

    /// <summary>An epic or stage, and the label its items' history uses ("MOB-1 Card refunds").</summary>
    private sealed record SeededContainer(long Id, string Label, SeededStatus Status);

    /// <summary>A live top-level card, with its row so its sprint or links can be set once the project's work is laid out.</summary>
    private sealed record SeededCard(long Id, bool Done, decimal? Estimate, DataRow Row);

    /// <summary>Generates one project's work at a time into tables that are bulk-copied in batches.</summary>
    private sealed class WorkWriter(string connectionString, IReadOnlyDictionary<Guid, string> names, Random random, DateTimeOffset now)
    {
        private readonly DataTable _items = ItemsTable();
        private readonly DataTable _changes = ChangesTable();
        private readonly DataTable _comments = CommentsTable();
        private readonly DataTable _sprints = SprintsTable();
        private readonly DataTable _scopeEvents = ScopeEventsTable();
        private readonly DataTable _gates = GatesTable();
        private readonly DataTable _gateApprovers = GateApproversTable();
        private readonly DataTable _gateRequests = GateRequestsTable();
        private readonly DataTable _gateDecisions = GateDecisionsTable();
        private readonly DataTable _dependencies = DependenciesTable();
        private readonly DataTable _attachments = AttachmentsTable();
        private readonly DataTable _attachmentContents = AttachmentContentsTable();
        private readonly DataTable _watchers = WatchersTable();
        private readonly DataTable _notifications = NotificationsTable();
        private readonly DataTable _searchTerms = SearchTermsTable();
        private readonly DataTable _itemLabels = WorkItemLabelsTable();
        private readonly Dictionary<long, HashSet<Guid>> _watching = [];

        /// <summary>Each item's title, description and comments, turned into search words when the batch is written.</summary>
        private readonly Dictionary<long, (long ProjectId, List<string> Texts)> _searchText = [];
        private List<Guid> _contributors = [];
        private long _nextItemId;
        private long _nextCommentId;
        private long _nextSprintId;
        private long _nextGateId;
        private long _nextGateRequestId;
        private long _nextAttachmentId;

        public int WorkItems { get; private set; }

        public int Changes { get; private set; }

        public int Comments { get; private set; }

        /// <summary>The shared labels, the most used first (Phase 4 research R26).</summary>
        public IReadOnlyList<(int Id, string Name)> Labels { get; init; } = [];

        public int PendingRows => _items.Rows.Count + _changes.Rows.Count + _comments.Rows.Count + _scopeEvents.Rows.Count + _gateApprovers.Rows.Count
            + _watchers.Rows.Count + _notifications.Rows.Count + _attachments.Rows.Count + _dependencies.Rows.Count + _itemLabels.Rows.Count;

        public async Task InitializeAsync(CancellationToken ct)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            _nextItemId = await NextIdAsync(connection, "WorkItems", ct);
            _nextCommentId = await NextIdAsync(connection, "Comments", ct);
            _nextSprintId = await NextIdAsync(connection, "Sprints", ct);
            _nextGateId = await NextIdAsync(connection, "StageGates", ct);
            _nextGateRequestId = await NextIdAsync(connection, "GateRequests", ct);
            _nextAttachmentId = await NextIdAsync(connection, "Attachments", ct);
        }

        public void AddProject(SeededProject project)
        {
            _contributors = project.Contributors;
            var toDo = project.Statuses.First(s => s.Category == StatusCategory.ToDo);
            var inProgress = project.Statuses.Where(s => s.Category == StatusCategory.InProgress).ToList();
            var done = project.Statuses.First(s => s.Category == StatusCategory.Done);
            var lastRank = new Dictionary<long, string?>();
            var number = 0;
            var parents = new List<(long Id, DateTimeOffset CreatedAt, string? LastChildRank)>();
            var cards = new List<SeededCard>();
            var containers = project.Template switch
            {
                ProjectTemplate.Scrum => AddEpics(project, Math.Max(2, project.Tasks / 40), ref number, toDo, inProgress, done),
                ProjectTemplate.StageGate => AddStages(project, ref number, toDo, inProgress, done),
                _ => [],
            };

            for (var t = 0; t < project.Tasks; t++)
            {
                var roll = random.Next(100);
                var status = roll < 25 ? toDo : roll < 40 ? inProgress[random.Next(inProgress.Count)] : done;
                DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done
                    ? now.AddMinutes(-(random.Next(100) < 3 ? random.Next(1, 14 * 24 * 60) : random.Next(14 * 24 * 60, 365 * 24 * 60)))
                    : null;
                var createdAt = (resolvedAt ?? now).AddHours(-random.Next(2, 2_000));
                var id = _nextItemId++;
                // Scrum projects keep one order for the whole project; the others order each column (research R8).
                var scope = project.Template == ProjectTemplate.Scrum ? 0 : status.Id;
                var rank = lastRank[scope] = Rank.After(lastRank.GetValueOrDefault(scope));
                var deleted = random.Next(100) == 0;
                var (type, container, estimate) = CardDetails(project.Template, containers);
                var updatedAt = AddItem(id, project, ++number, type, null, status, rank, createdAt, resolvedAt, deleted,
                    toDo, inProgress[0], container, estimate);
                if (deleted)
                {
                    continue;
                }

                cards.Add(new SeededCard(id, resolvedAt is not null, estimate, _items.Rows[_items.Rows.Count - 1]));
                parents.Add((id, createdAt, null));

                // Five comments a card on average, about 2,000,000 at the baseline (Phase 4 research R26).
                AddComments(project.Id, id, createdAt, updatedAt, random.Next(0, 11));
            }

            for (var s = 0; s < project.Subtasks && parents.Count > 0; s++)
            {
                var index = random.Next(parents.Count);
                var parent = parents[index];
                var status = random.Next(2) == 0 ? toDo : done;
                var createdAt = parent.CreatedAt.AddHours(random.Next(1, 48));
                DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done ? createdAt.AddHours(random.Next(1, 200)) : null;
                if (resolvedAt > now)
                {
                    resolvedAt = now;
                }

                var rank = Rank.After(parent.LastChildRank);
                parents[index] = parent with { LastChildRank = rank };
                var id = _nextItemId++;
                var key = $"{project.Key}-{++number}";
                AddItem(id, project, number, WorkItemType.Subtask, parent.Id, status, rank, createdAt, resolvedAt, false,
                    toDo, inProgress[0]);
                AddChange(parent.Id, createdAt, WorkItemField.SubtaskAdded, null, key, "Sub-task");
            }

            if (project.Template == ProjectTemplate.Scrum)
            {
                AddSprints(project, cards);
            }
            else if (project.Template == ProjectTemplate.StageGate)
            {
                AddDependencies(project, cards);
            }
        }

        /// <summary>About 50 links between a Stage-gate project's tasks, each later task waiting for an earlier one, so
        /// there is no loop; each link has its history row on both items (Phase 3 research R25).</summary>
        private void AddDependencies(SeededProject project, List<SeededCard> cards)
        {
            if (cards.Count < 2)
            {
                return;
            }

            var links = new HashSet<(int, int)>();
            for (var tries = 0; links.Count < Math.Min(50, cards.Count - 1) && tries < 500; tries++)
            {
                var waitedFor = random.Next(cards.Count - 1);
                var waiting = random.Next(waitedFor + 1, Math.Min(cards.Count, waitedFor + 30));
                if (!links.Add((waitedFor, waiting)))
                {
                    continue;
                }

                var (a, b) = (cards[waitedFor], cards[waiting]);
                var (createdA, createdB) = ((DateTimeOffset)a.Row["CreatedAt"], (DateTimeOffset)b.Row["CreatedAt"]);
                var at = Later(createdA > createdB ? createdA : createdB, null);
                var actor = RandomContributor();
                var changeSet = Seeder.NewGuid(random);
                _dependencies.Rows.Add(project.Id, a.Id, b.Id, actor, at);
                AddChange(b.Id, at, WorkItemField.DependencyAdded, null, $"waits for {a.Row["Key"]} {a.Row["Title"]}", null, actor, changeSet);
                AddChange(a.Id, at, WorkItemField.DependencyAdded, null, $"is waited for by {b.Row["Key"]} {b.Row["Title"]}", null, actor, changeSet);
            }
        }

        /// <summary>Three completed two-week sprints of done cards, the active sprint that started a week ago with the top of
        /// the open cards and a few done ones, and a planned sprint with the next open cards; the rest is the backlog
        /// (Phase 3 research R25). Each card that joined a sprint has its history row and each sprint its scope log.</summary>
        private void AddSprints(SeededProject project, List<SeededCard> cards)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var activeStart = today.AddDays(-7);
            var owner = _contributors[0];
            var doneCards = cards.Where(c => c.Done).ToList();
            var openCards = cards.Where(c => !c.Done).ToList();
            var perCompleted = Math.Min(20, doneCards.Count / 4);
            for (var n = 1; n <= 5; n++)
            {
                var state = n switch { <= 3 => SprintState.Completed, 4 => SprintState.Active, _ => SprintState.Planned };
                var id = _nextSprintId++;
                var name = $"{project.Key} Sprint {n}";
                DateOnly? start = n <= 4 ? activeStart.AddDays(-14 * (4 - n)) : null;
                DateOnly? end = start?.AddDays(Sprint.DefaultLengthDays);
                var startedAt = start is { } s ? new DateTimeOffset(s.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero) : (DateTimeOffset?)null;
                var completedAt = state == SprintState.Completed ? new DateTimeOffset(end!.Value.ToDateTime(new TimeOnly(16, 0)), TimeSpan.Zero) : (DateTimeOffset?)null;
                _sprints.Rows.Add(id, project.Id, n, name, n == 4 ? "Ship the next release" : DBNull.Value, state.ToString(),
                    (object?)start?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value, (object?)end?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value,
                    (object?)startedAt ?? DBNull.Value, (object?)completedAt ?? DBNull.Value, (startedAt ?? now).AddDays(-1), owner);

                var members = state switch
                {
                    SprintState.Completed => doneCards.Skip((n - 1) * perCompleted).Take(perCompleted).ToList(),
                    SprintState.Active => [.. openCards.Take(12), .. doneCards.Skip(3 * perCompleted).Take(4)],
                    _ => openCards.Skip(12).Take(10).ToList(),
                };
                foreach (var card in members)
                {
                    card.Row["SprintId"] = id;
                    AddChange(card.Id, (startedAt ?? now).AddDays(-1), WorkItemField.Sprint, null, name, null, owner);
                    if (startedAt is { } started)
                    {
                        AddScopeEvent(id, card, SprintScopeKind.Committed, started, owner);
                    }

                    if (completedAt is { } completed)
                    {
                        AddScopeEvent(id, card, SprintScopeKind.Completed, completed, owner);
                    }
                }
            }
        }

        private void AddScopeEvent(long sprintId, SeededCard card, SprintScopeKind kind, DateTimeOffset at, Guid actor) =>
            _scopeEvents.Rows.Add(sprintId, card.Id, kind.ToString(), (object?)card.Estimate ?? DBNull.Value, at, actor);

        /// <summary>Epics at the start of a Scrum project's numbers: most open, some done.</summary>
        private List<SeededContainer> AddEpics(SeededProject project, int count, ref int number, SeededStatus toDo,
            List<SeededStatus> inProgress, SeededStatus done)
        {
            var epics = new List<SeededContainer>(count);
            for (var e = 0; e < count; e++)
            {
                var roll = random.Next(100);
                var status = roll < 40 ? toDo : roll < 80 ? inProgress[0] : done;
                var createdAt = now.AddHours(-random.Next(2_000, 6_000));
                DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done ? Later(createdAt, null) : null;
                var id = _nextItemId++;
                var title = SampleText.Title(random);
                AddItem(id, project, ++number, WorkItemType.Epic, null, status, Rank.First(), createdAt, resolvedAt, false,
                    toDo, inProgress[0], title: title);
                epics.Add(new SeededContainer(id, $"{project.Key}-{number} {title}", status));
            }

            return epics;
        }

        /// <summary>The four stages of a Stage-gate project in order (FR-003), the first completed and the second in
        /// progress, each with one or two milestones.</summary>
        private List<SeededContainer> AddStages(SeededProject project, ref int number, SeededStatus toDo,
            List<SeededStatus> inProgress, SeededStatus done)
        {
            string[] names = ["Initiation", "Planning", "Execution", "Closure"];
            var ranks = Rank.Sequence(names.Length);
            var createdAt = now.AddHours(-random.Next(3_000, 6_000));
            var stages = new List<SeededContainer>(names.Length);
            for (var s = 0; s < names.Length; s++)
            {
                // Initiation and Planning passed their gates, Execution is current and Closure is locked (Phase 3 FR-032).
                var status = s <= 1 ? done : s == 2 ? inProgress[0] : toDo;
                DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done ? Later(createdAt, now.AddHours(-1_000)) : null;
                var id = _nextItemId++;
                AddItem(id, project, ++number, WorkItemType.Stage, null, status, ranks[s], createdAt, resolvedAt, false,
                    toDo, inProgress[0], title: names[s]);
                stages.Add(new SeededContainer(id, $"{project.Key}-{number} {names[s]}", status));
            }

            foreach (var stage in stages)
            {
                for (var m = random.Next(1, 3); m > 0; m--)
                {
                    var status = stage.Status.Category == StatusCategory.Done ? done : toDo;
                    var at = createdAt.AddHours(random.Next(1, 48));
                    DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done ? Later(at, now.AddHours(-500)) : null;
                    AddItem(_nextItemId++, project, ++number, WorkItemType.Milestone, null, status, Rank.First(), at, resolvedAt,
                        false, toDo, inProgress[0], stage);
                }
            }

            AddGates(project, stages, createdAt);
            return stages;
        }

        /// <summary>Each stage's gate, with two of the team as approvers and one approval needed: Initiation's and Planning's
        /// approved, Execution's awaiting a decision (or, in every other project, sent back with a note), and Closure's not
        /// requested; each step has its history row on the stage (Phase 3 FR-040, research R25).</summary>
        private void AddGates(SeededProject project, List<SeededContainer> stages, DateTimeOffset createdAt)
        {
            var approvers = _contributors.Take(2).ToList();
            var requester = _contributors[_contributors.Count > 2 ? 2 : 0];
            var approverNames = string.Join(", ", approvers.Select(a => names[a]).Order(StringComparer.CurrentCultureIgnoreCase));
            for (var s = 0; s < stages.Count; s++)
            {
                var stage = stages[s];
                var gateId = _nextGateId++;
                var at = createdAt.AddHours(1 + s);
                var state = s switch
                {
                    <= 1 => GateState.Approved,
                    2 => project.Id % 2 == 0 ? GateState.AwaitingDecision : GateState.SentBack,
                    _ => GateState.NotRequested,
                };
                long? requestId = state == GateState.NotRequested ? null : _nextGateRequestId++;
                DateTimeOffset? approvedAt = state == GateState.Approved ? at.AddHours(3) : null;
                _gates.Rows.Add(gateId, project.Id, stage.Id, 1, true, state.ToString(), (object?)requestId ?? DBNull.Value,
                    (object?)approvedAt ?? DBNull.Value);
                foreach (var approver in approvers)
                {
                    _gateApprovers.Rows.Add(gateId, approver, at, approvers[0]);
                }

                AddChange(stage.Id, at, WorkItemField.GateSetup, null,
                    $"Approvers: {approverNames} · approvals needed: 1 · the requester can't approve", null, approvers[0]);
                if (requestId is not { } request)
                {
                    continue;
                }

                var requestedAt = at.AddHours(1);
                var decidedAt = requestedAt.AddHours(2);
                var outcome = state switch
                {
                    GateState.Approved => GateOutcome.Approved,
                    GateState.SentBack => GateOutcome.SentBack,
                    _ => GateOutcome.Pending,
                };
                _gateRequests.Rows.Add(request, gateId, 1, requester, requestedAt, outcome.ToString(),
                    outcome == GateOutcome.Pending ? DBNull.Value : decidedAt, outcome == GateOutcome.Pending ? DBNull.Value : approvers[0]);
                AddChange(stage.Id, requestedAt, WorkItemField.GateRequested, null, "Request 1", null, requester);
                if (outcome == GateOutcome.Pending)
                {
                    continue;
                }

                var sentBack = outcome == GateOutcome.SentBack;
                var note = sentBack ? "The budget is missing." : null;
                _gateDecisions.Rows.Add(request, approvers[0], sentBack ? nameof(GateDecisionKind.SendBack) : nameof(GateDecisionKind.Approve),
                    (object?)note ?? DBNull.Value, decidedAt);
                AddChange(stage.Id, decidedAt, WorkItemField.GateDecision, "Request 1", sentBack ? "Sent back" : "Approved", note, approvers[0]);
                if (!sentBack)
                {
                    AddChange(stage.Id, decidedAt, WorkItemField.GateApproved, null, "Approved", null, approvers[0]);
                }
            }
        }

        /// <summary>Scrum cards are about 55% stories, 25% tasks and 20% bugs; 60% are in an epic and 70% estimated.
        /// Most Stage-gate tasks are in a stage. Kanban projects keep the tasks of Phase 2.</summary>
        private (WorkItemType Type, SeededContainer? Container, decimal? Estimate) CardDetails(ProjectTemplate template,
            List<SeededContainer> containers)
        {
            decimal[] points = [1, 2, 3, 5, 8, 13];
            return template switch
            {
                ProjectTemplate.Scrum => (
                    random.Next(100) switch { < 55 => WorkItemType.Story, < 80 => WorkItemType.Task, _ => WorkItemType.Bug },
                    random.Next(100) < 60 ? containers[random.Next(containers.Count)] : null,
                    random.Next(100) < 70 ? points[random.Next(points.Length)] : null),
                ProjectTemplate.StageGate => (WorkItemType.Task,
                    random.Next(100) < 85 ? containers[random.Next(containers.Count - 1)] : null, null), // the locked Closure stage has no work yet
                _ => (WorkItemType.Task, null, null),
            };
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            // Parents before children: sprints, work items, then the comments, history rows and scope events that refer to them.
            await CopyAsync(_sprints, "Sprints", keepIdentity: true, ct);
            await CopyAsync(_items, "WorkItems", keepIdentity: true, ct);
            await CopyAsync(_gates, "StageGates", keepIdentity: true, ct);
            await CopyAsync(_gateApprovers, "GateApprovers", keepIdentity: false, ct);
            await CopyAsync(_gateRequests, "GateRequests", keepIdentity: true, ct);
            await CopyAsync(_gateDecisions, "GateDecisions", keepIdentity: false, ct);
            await CopyAsync(_dependencies, "Dependencies", keepIdentity: false, ct);
            await CopyAsync(_comments, "Comments", keepIdentity: true, ct);
            await CopyAsync(_changes, "WorkItemChanges", keepIdentity: false, ct);
            await CopyAsync(_scopeEvents, "SprintScopeEvents", keepIdentity: false, ct);
            await CopyAsync(_attachments, "Attachments", keepIdentity: true, ct);
            await CopyAsync(_attachmentContents, "AttachmentContents", keepIdentity: false, ct);
            await CopyAsync(_itemLabels, "WorkItemLabels", keepIdentity: false, ct);
            await CopyAsync(_watchers, "WorkItemWatchers", keepIdentity: false, ct);
            await CopyAsync(_notifications, "Notifications", keepIdentity: false, ct);
            _watching.Clear();

            // Every item is already indexed, as SearchIndexer would leave it; the progress row says so (Phase 4 research R15).
            foreach (var (workItemId, (projectId, texts)) in _searchText)
            {
                foreach (var term in SearchWords.Of(string.Join('\n', texts)))
                {
                    _searchTerms.Rows.Add(workItemId, term, projectId);
                }

                if (_searchTerms.Rows.Count >= 100_000)
                {
                    await CopyAsync(_searchTerms, "SearchTerms", keepIdentity: false, ct);
                }
            }

            await CopyAsync(_searchTerms, "SearchTerms", keepIdentity: false, ct);
            _searchText.Clear();
        }

        /// <summary>Adds the work item and its history; returns its last change time.</summary>
        private DateTimeOffset AddItem(long id, SeededProject project, int number, WorkItemType type, long? parentId,
            SeededStatus status, string rank, DateTimeOffset createdAt, DateTimeOffset? resolvedAt, bool deleted,
            SeededStatus toDo, SeededStatus inProgress, SeededContainer? container = null, decimal? estimate = null,
            string? title = null)
        {
            var creator = RandomContributor();
            var priority = RandomPriority();
            title ??= SampleText.Title(random);
            AddChange(id, createdAt, WorkItemField.Created, null, toDo.Name, null, creator);
            if (container is not null)
            {
                AddChange(id, createdAt, WorkItemField.Container, null, container.Label, null, creator);
            }

            var at = createdAt;
            if (status.Category != StatusCategory.ToDo)
            {
                var firstStep = status.Category == StatusCategory.Done ? inProgress : status;
                at = Later(at, resolvedAt);
                AddChange(id, at, WorkItemField.Status, toDo.Name, firstStep.Name, null);
                if (status.Category == StatusCategory.Done)
                {
                    at = resolvedAt!.Value;
                    AddChange(id, at, WorkItemField.Status, firstStep.Name, status.Name, null);
                }
            }

            if (priority != Priority.Medium && random.Next(100) < 30)
            {
                AddChange(id, Later(createdAt, at), WorkItemField.Priority, nameof(Priority.Medium), priority.ToString(), null);
            }

            Guid? assignee = random.Next(100) < 70 ? RandomContributor() : null;
            if (assignee is { } person)
            {
                AddChange(id, Later(createdAt, at), WorkItemField.Assignee, null, names[person], null);
            }

            if (estimate is { } points)
            {
                AddChange(id, Later(createdAt, at), WorkItemField.Estimate, null, WorkItem.FormatEstimate(points), null);
            }

            // Both dates are one edit, as in the drawer: one change set (Phase 2 research R8). A milestone has one date,
            // its due date (FR-021).
            var (start, due) = RandomDates(createdAt);
            if (type == WorkItemType.Milestone)
            {
                (start, due) = (null, due ?? DateOnly.FromDateTime(createdAt.UtcDateTime).AddDays(random.Next(14, 90)));
            }

            if (due is not null)
            {
                var scheduledAt = Later(createdAt, at);
                var changeSet = Seeder.NewGuid(random);
                if (start is { } startDate)
                {
                    AddChange(id, scheduledAt, WorkItemField.StartDate, null, Iso(startDate), null, changeSetId: changeSet);
                }

                AddChange(id, scheduledAt, WorkItemField.DueDate, null, Iso(due.Value), null, changeSetId: changeSet);
            }

            DateTimeOffset? deletedAt = deleted ? Later(at, null) : null;
            Guid? deletedBy = deleted ? RandomContributor() : null;
            if (deletedAt is { } when)
            {
                at = when;
                AddChange(id, when, WorkItemField.Deleted, null, null, null, deletedBy);
            }

            // Phase 1–3 descriptions are plain text, which is also their plain-text copy (Phase 4 research R4).
            var description = SampleText.Description(random);
            _items.Rows.Add(id, project.Id, number, $"{project.Key}-{number}", type.ToString(), title,
                (object?)description ?? DBNull.Value, priority.ToString(), status.Id, rank,
                (object?)parentId ?? DBNull.Value, creator, createdAt, at, (object?)resolvedAt ?? DBNull.Value, deleted,
                (object?)deletedAt ?? DBNull.Value, (object?)deletedBy ?? DBNull.Value, (object?)assignee ?? DBNull.Value,
                (object?)start?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value, (object?)due?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value,
                (object?)container?.Id ?? DBNull.Value, (object?)estimate ?? DBNull.Value, DBNull.Value, (object?)description ?? DBNull.Value,
                type == WorkItemType.Epic ? (object)(byte)EpicColors.ForNumber(number) : DBNull.Value); // epics take their colors in turn (FR-025)
            _searchText[id] = (project.Id, [title, description ?? ""]);
            WorkItems++;

            // The creator and the assignee watch the item; a recent assignment is a notification (Phase 4 FR-011, FR-015).
            Watch(id, creator, createdAt);
            if (random.Next(100) < 30 && Labels.Count > 0)
            {
                AddLabels(id, creator, Later(createdAt, at));
            }

            if (!deleted && random.Next(100) < 10)
            {
                Attach(id, creator, Later(createdAt, at));
            }

            if (assignee is { } assigned)
            {
                Watch(id, assigned, createdAt);
                Notify(assigned, NotificationKind.Assigned, project.Id, id, creator, null, Later(createdAt, at));
            }
            return at;
        }

        private void AddComments(long projectId, long workItemId, DateTimeOffset from, DateTimeOffset to, int count)
        {
            for (var c = count; c > 0; c--)
            {
                var id = _nextCommentId++;
                var author = RandomContributor();
                var at = Later(from, to > from ? to : null);
                var body = SampleText.Comment(random);
                _comments.Rows.Add(id, workItemId, author, body, at, DBNull.Value, false, DBNull.Value, body);
                _searchText[workItemId].Texts.Add(body);
                AddChange(workItemId, at, WorkItemField.CommentAdded, null, body, $"comment {id}", author);
                Comments++;

                // The other watchers hear about it; the author starts watching (Phase 4 FR-011, FR-015).
                foreach (var watcher in _watching[workItemId].Where(w => w != author))
                {
                    Notify(watcher, NotificationKind.Commented, projectId, workItemId, author, null, at);
                }

                Watch(workItemId, author, at);
            }
        }

        /// <summary>One to three labels; a few labels are on many items and most on few, as in real projects.</summary>
        private void AddLabels(long workItemId, Guid by, DateTimeOffset at)
        {
            var chosen = new HashSet<int>();
            for (var n = random.Next(1, 4); n > 0; n--)
            {
                var label = Labels[(int)(Math.Pow(random.NextDouble(), 2) * Labels.Count)];
                if (chosen.Add(label.Id))
                {
                    _itemLabels.Rows.Add(workItemId, label.Id, at, by);
                    AddChange(workItemId, at, WorkItemField.LabelAdded, null, label.Name, null, by);
                }
            }
        }

        /// <summary>A small PDF that has passed its malware check, with its bytes (Phase 4 research R26).</summary>
        private void Attach(long workItemId, Guid uploader, DateTimeOffset at)
        {
            var id = _nextAttachmentId++;
            var content = Encoding.ASCII.GetBytes($"%PDF-1.7\n% U-PMS seed attachment {id}\n%%EOF\n");
            _attachments.Rows.Add(id, workItemId, $"notes-{id}.pdf", "pdf", "application/octet-stream", (long)content.Length,
                SHA256.HashData(content), uploader, at, "Passed", 1, at);
            _attachmentContents.Rows.Add(id, content);
            AddChange(workItemId, at, WorkItemField.AttachmentAdded, null, $"notes-{id}.pdf", null, uploader);
        }

        private void Watch(long workItemId, Guid userId, DateTimeOffset at)
        {
            if (!_watching.TryGetValue(workItemId, out var people))
            {
                _watching[workItemId] = people = [];
            }

            if (people.Add(userId))
            {
                _watchers.Rows.Add(workItemId, userId, at);
            }
        }

        /// <summary>Notifications are kept for 90 days (FR-021), so only recent events have one; most have been read.</summary>
        private void Notify(Guid recipient, NotificationKind kind, long projectId, long workItemId, Guid actor, string? detail,
            DateTimeOffset at)
        {
            if (at < now.AddDays(-90))
            {
                return;
            }

            _notifications.Rows.Add(recipient, kind.ToString(), projectId, workItemId, actor, (object?)detail ?? DBNull.Value, at,
                random.Next(100) < 70 ? (object)at.AddHours(random.Next(1, 48)) : DBNull.Value);
        }

        private void AddChange(long workItemId, DateTimeOffset at, WorkItemField field, string? oldValue, string? newValue,
            string? note, Guid? actor = null, Guid? changeSetId = null)
        {
            _changes.Rows.Add(workItemId, changeSetId ?? Seeder.NewGuid(random), actor ?? RandomContributor(), at, field.ToString(),
                (object?)oldValue ?? DBNull.Value, (object?)newValue ?? DBNull.Value, (object?)note ?? DBNull.Value);
            Changes++;
        }

        /// <summary>A moment after <paramref name="from"/> and not after <paramref name="limit"/> (or now).</summary>
        private DateTimeOffset Later(DateTimeOffset from, DateTimeOffset? limit)
        {
            var end = limit ?? now;
            var span = (end - from).TotalMinutes;
            return span <= 1 ? end : from.AddMinutes(random.NextDouble() * span);
        }

        private Guid RandomContributor() => _contributors[random.Next(_contributors.Count)];

        /// <summary>About half of the work has a due date within two months of its creation; most of that also has a
        /// start date up to two weeks before it.</summary>
        private (DateOnly? Start, DateOnly? Due) RandomDates(DateTimeOffset createdAt)
        {
            if (random.Next(2) == 0)
            {
                return (null, null);
            }

            var due = DateOnly.FromDateTime(createdAt.UtcDateTime).AddDays(random.Next(1, 60));
            return (random.Next(100) < 60 ? due.AddDays(-random.Next(0, 15)) : null, due);
        }

        private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private Priority RandomPriority() => random.Next(100) switch
        {
            < 5 => Priority.Highest,
            < 25 => Priority.High,
            < 75 => Priority.Medium,
            < 90 => Priority.Low,
            _ => Priority.Lowest,
        };

        private async Task CopyAsync(DataTable table, string destination, bool keepIdentity, CancellationToken ct)
        {
            if (table.Rows.Count == 0)
            {
                return;
            }

            var options = SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.TableLock
                | (keepIdentity ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default);
            using var copy = new SqlBulkCopy(connectionString, options)
            {
                DestinationTableName = destination,
                BatchSize = 5_000,
                BulkCopyTimeout = 600,
            };
            foreach (DataColumn column in table.Columns)
            {
                copy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await copy.WriteToServerAsync(table, ct);
            table.Clear();
        }

        private static async Task<long> NextIdAsync(SqlConnection connection, string table, CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT ISNULL(MAX(Id), 0) + 1 FROM {table}";
            return Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        }

        private static DataTable ItemsTable() => Table(
            ("Id", typeof(long)), ("ProjectId", typeof(long)), ("Number", typeof(int)), ("Key", typeof(string)),
            ("Type", typeof(string)), ("Title", typeof(string)), ("Description", typeof(string)), ("Priority", typeof(string)),
            ("StatusId", typeof(long)), ("Rank", typeof(string)), ("ParentId", typeof(long)), ("CreatedById", typeof(Guid)),
            ("CreatedAt", typeof(DateTimeOffset)), ("UpdatedAt", typeof(DateTimeOffset)), ("ResolvedAt", typeof(DateTimeOffset)),
            ("IsDeleted", typeof(bool)), ("DeletedAt", typeof(DateTimeOffset)), ("DeletedById", typeof(Guid)),
            ("AssigneeId", typeof(Guid)), ("StartDate", typeof(DateTime)), ("DueDate", typeof(DateTime)),
            ("ContainerId", typeof(long)), ("Estimate", typeof(decimal)), ("SprintId", typeof(long)), ("DescriptionText", typeof(string)),
            ("Color", typeof(byte)));

        private static DataTable ChangesTable() => Table(
            ("WorkItemId", typeof(long)), ("ChangeSetId", typeof(Guid)), ("ActorId", typeof(Guid)), ("OccurredAt", typeof(DateTimeOffset)),
            ("Field", typeof(string)), ("OldValue", typeof(string)), ("NewValue", typeof(string)), ("Note", typeof(string)));

        private static DataTable SprintsTable() => Table(
            ("Id", typeof(long)), ("ProjectId", typeof(long)), ("Number", typeof(int)), ("Name", typeof(string)), ("Goal", typeof(string)),
            ("State", typeof(string)), ("StartDate", typeof(DateTime)), ("EndDate", typeof(DateTime)), ("StartedAt", typeof(DateTimeOffset)),
            ("CompletedAt", typeof(DateTimeOffset)), ("CreatedAt", typeof(DateTimeOffset)), ("CreatedById", typeof(Guid)));

        private static DataTable GatesTable() => Table(
            ("Id", typeof(long)), ("ProjectId", typeof(long)), ("StageId", typeof(long)), ("RequiredApprovals", typeof(int)),
            ("ExcludeRequester", typeof(bool)), ("State", typeof(string)), ("CurrentRequestId", typeof(long)), ("ApprovedAt", typeof(DateTimeOffset)));

        private static DataTable GateApproversTable() => Table(
            ("GateId", typeof(long)), ("UserId", typeof(Guid)), ("AddedAt", typeof(DateTimeOffset)), ("AddedById", typeof(Guid)));

        private static DataTable GateRequestsTable() => Table(
            ("Id", typeof(long)), ("GateId", typeof(long)), ("Number", typeof(int)), ("RequestedById", typeof(Guid)),
            ("RequestedAt", typeof(DateTimeOffset)), ("Outcome", typeof(string)), ("ClosedAt", typeof(DateTimeOffset)), ("ClosedById", typeof(Guid)));

        private static DataTable GateDecisionsTable() => Table(
            ("RequestId", typeof(long)), ("ApproverId", typeof(Guid)), ("Decision", typeof(string)), ("Note", typeof(string)),
            ("DecidedAt", typeof(DateTimeOffset)));

        private static DataTable DependenciesTable() => Table(
            ("ProjectId", typeof(long)), ("PredecessorId", typeof(long)), ("SuccessorId", typeof(long)), ("CreatedById", typeof(Guid)),
            ("CreatedAt", typeof(DateTimeOffset)));

        private static DataTable ScopeEventsTable() => Table(
            ("SprintId", typeof(long)), ("WorkItemId", typeof(long)), ("Kind", typeof(string)), ("Estimate", typeof(decimal)),
            ("OccurredAt", typeof(DateTimeOffset)), ("ActorId", typeof(Guid)));

        private static DataTable CommentsTable() => Table(
            ("Id", typeof(long)), ("WorkItemId", typeof(long)), ("AuthorId", typeof(Guid)), ("Body", typeof(string)),
            ("CreatedAt", typeof(DateTimeOffset)), ("EditedAt", typeof(DateTimeOffset)), ("IsDeleted", typeof(bool)),
            ("DeletedAt", typeof(DateTimeOffset)), ("BodyText", typeof(string)));

        private static DataTable AttachmentsTable() => Table(
            ("Id", typeof(long)), ("WorkItemId", typeof(long)), ("FileName", typeof(string)), ("Extension", typeof(string)),
            ("ContentType", typeof(string)), ("Size", typeof(long)), ("Sha256", typeof(byte[])), ("UploadedById", typeof(Guid)),
            ("UploadedAt", typeof(DateTimeOffset)), ("CheckStatus", typeof(string)), ("CheckAttempts", typeof(int)), ("CheckedAt", typeof(DateTimeOffset)));

        private static DataTable AttachmentContentsTable() => Table(("AttachmentId", typeof(long)), ("Content", typeof(byte[])));

        private static DataTable WorkItemLabelsTable() => Table(
            ("WorkItemId", typeof(long)), ("LabelId", typeof(int)), ("AddedAt", typeof(DateTimeOffset)), ("AddedById", typeof(Guid)));

        private static DataTable SearchTermsTable() => Table(("WorkItemId", typeof(long)), ("Term", typeof(string)), ("ProjectId", typeof(long)));

        private static DataTable WatchersTable() => Table(
            ("WorkItemId", typeof(long)), ("UserId", typeof(Guid)), ("StartedAt", typeof(DateTimeOffset)));

        private static DataTable NotificationsTable() => Table(
            ("RecipientId", typeof(Guid)), ("Kind", typeof(string)), ("ProjectId", typeof(long)), ("WorkItemId", typeof(long)),
            ("ActorId", typeof(Guid)), ("Detail", typeof(string)), ("OccurredAt", typeof(DateTimeOffset)), ("ReadAt", typeof(DateTimeOffset)));

        private static DataTable Table(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable { Locale = CultureInfo.InvariantCulture };
            foreach (var (name, type) in columns)
            {
                table.Columns.Add(name, type);
            }

            return table;
        }
    }
}
