using System.Data;
using System.Diagnostics;
using System.Globalization;
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
/// do, 15% in progress and 60% done (3% of those in the last 14 days), and 1% are deleted. Every generated change
/// has a matching history row.</summary>
public sealed class Seeder(string connectionString, Action<string> log)
{
    private const int BatchRows = 20_000;

    /// <summary>Members of the largest project: enough for every tenth simulated user of the SC-002 run to work on it
    /// as a different person.</summary>
    private const int LargestProjectMembers = 40;

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
        var work = new WorkWriter(connectionString, random, now);
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
                CreatedAt = now.AddDays(-random.Next(30, 400)),
            };
            db.Users.Add(user);
            ids.Add(user.Id);
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
                    users[random.Next(users.Count)], createdAt).Value!;
                AddTeam(project, users, random, i == 0 ? LargestProjectMembers : random.Next(4, 21), random.Next(0, 4), createdAt);
                memberships += project.Members.Count;
                if (i % 5 == 0)
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
        return created.ConvertAll(c => new SeededProject(c.Project.Id, c.Project.Key, c.Tasks,
            (int)Math.Round(c.Tasks * subtaskRatio),
            c.Project.Statuses.Select(s => new SeededStatus(s.Id, s.Name, s.Category)).ToList(),
            c.Project.Members.Where(m => m.Role != ProjectRole.Viewer).Select(m => m.UserId).ToList()));
    }

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
            """;
        await command.ExecuteNonQueryAsync(ct);
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
    private sealed record SeededProject(long Id, string Key, int Tasks, int Subtasks, List<SeededStatus> Statuses,
        List<Guid> Contributors);

    /// <summary>Generates one project's work at a time into tables that are bulk-copied in batches.</summary>
    private sealed class WorkWriter(string connectionString, Random random, DateTimeOffset now)
    {
        private readonly DataTable _items = ItemsTable();
        private readonly DataTable _changes = ChangesTable();
        private readonly DataTable _comments = CommentsTable();
        private List<Guid> _contributors = [];
        private long _nextItemId;
        private long _nextCommentId;

        public int WorkItems { get; private set; }

        public int Changes { get; private set; }

        public int Comments { get; private set; }

        public int PendingRows => _items.Rows.Count + _changes.Rows.Count + _comments.Rows.Count;

        public async Task InitializeAsync(CancellationToken ct)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            _nextItemId = await NextIdAsync(connection, "WorkItems", ct);
            _nextCommentId = await NextIdAsync(connection, "Comments", ct);
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

            for (var t = 0; t < project.Tasks; t++)
            {
                var roll = random.Next(100);
                var status = roll < 25 ? toDo : roll < 40 ? inProgress[random.Next(inProgress.Count)] : done;
                DateTimeOffset? resolvedAt = status.Category == StatusCategory.Done
                    ? now.AddMinutes(-(random.Next(100) < 3 ? random.Next(1, 14 * 24 * 60) : random.Next(14 * 24 * 60, 365 * 24 * 60)))
                    : null;
                var createdAt = (resolvedAt ?? now).AddHours(-random.Next(2, 2_000));
                var id = _nextItemId++;
                var rank = lastRank[status.Id] = Rank.After(lastRank.GetValueOrDefault(status.Id));
                var deleted = random.Next(100) == 0;
                var updatedAt = AddItem(id, project, ++number, WorkItemType.Task, null, status, rank, createdAt, resolvedAt, deleted,
                    toDo, inProgress[0]);
                if (deleted)
                {
                    continue;
                }

                parents.Add((id, createdAt, null));
                if (random.Next(100) < 25)
                {
                    AddComments(id, createdAt, updatedAt);
                }
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
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            // Parents before children: work items, then the comments and history rows that refer to them.
            await CopyAsync(_items, "WorkItems", keepIdentity: true, ct);
            await CopyAsync(_comments, "Comments", keepIdentity: true, ct);
            await CopyAsync(_changes, "WorkItemChanges", keepIdentity: false, ct);
        }

        /// <summary>Adds the work item and its history; returns its last change time.</summary>
        private DateTimeOffset AddItem(long id, SeededProject project, int number, WorkItemType type, long? parentId,
            SeededStatus status, string rank, DateTimeOffset createdAt, DateTimeOffset? resolvedAt, bool deleted,
            SeededStatus toDo, SeededStatus inProgress)
        {
            var creator = RandomContributor();
            var priority = RandomPriority();
            var title = SampleText.Title(random);
            AddChange(id, createdAt, WorkItemField.Created, null, toDo.Name, null, creator);
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

            DateTimeOffset? deletedAt = deleted ? Later(at, null) : null;
            Guid? deletedBy = deleted ? RandomContributor() : null;
            if (deletedAt is { } when)
            {
                at = when;
                AddChange(id, when, WorkItemField.Deleted, null, null, null, deletedBy);
            }

            _items.Rows.Add(id, project.Id, number, $"{project.Key}-{number}", type.ToString(), title,
                (object?)SampleText.Description(random) ?? DBNull.Value, priority.ToString(), status.Id, rank,
                (object?)parentId ?? DBNull.Value, creator, createdAt, at, (object?)resolvedAt ?? DBNull.Value, deleted,
                (object?)deletedAt ?? DBNull.Value, (object?)deletedBy ?? DBNull.Value);
            WorkItems++;
            return at;
        }

        private void AddComments(long workItemId, DateTimeOffset from, DateTimeOffset to)
        {
            for (var c = random.Next(1, 4); c > 0; c--)
            {
                var id = _nextCommentId++;
                var author = RandomContributor();
                var at = Later(from, to > from ? to : null);
                var body = SampleText.Comment(random);
                _comments.Rows.Add(id, workItemId, author, body, at, DBNull.Value, false, DBNull.Value);
                AddChange(workItemId, at, WorkItemField.CommentAdded, null, body, $"comment {id}", author);
                Comments++;
            }
        }

        private void AddChange(long workItemId, DateTimeOffset at, WorkItemField field, string? oldValue, string? newValue,
            string? note, Guid? actor = null)
        {
            _changes.Rows.Add(workItemId, Seeder.NewGuid(random), actor ?? RandomContributor(), at, field.ToString(),
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
            ("IsDeleted", typeof(bool)), ("DeletedAt", typeof(DateTimeOffset)), ("DeletedById", typeof(Guid)));

        private static DataTable ChangesTable() => Table(
            ("WorkItemId", typeof(long)), ("ChangeSetId", typeof(Guid)), ("ActorId", typeof(Guid)), ("OccurredAt", typeof(DateTimeOffset)),
            ("Field", typeof(string)), ("OldValue", typeof(string)), ("NewValue", typeof(string)), ("Note", typeof(string)));

        private static DataTable CommentsTable() => Table(
            ("Id", typeof(long)), ("WorkItemId", typeof(long)), ("AuthorId", typeof(Guid)), ("Body", typeof(string)),
            ("CreatedAt", typeof(DateTimeOffset)), ("EditedAt", typeof(DateTimeOffset)), ("IsDeleted", typeof(bool)),
            ("DeletedAt", typeof(DateTimeOffset)));

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
