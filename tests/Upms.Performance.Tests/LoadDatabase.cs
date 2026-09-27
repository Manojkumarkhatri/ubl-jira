using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Infrastructure.Persistence;
using Upms.Seed;

namespace Upms.Performance.Tests;

/// <summary>A database with the SC-002 volumes: the one named by <c>UPMS_PERF_CONNECTION</c>, or a SQL Server
/// container. An empty database is migrated and seeded with <see cref="Seeder"/> (tools/Upms.Seed).</summary>
public sealed class LoadDatabase : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public LoadTestSettings Settings { get; } = LoadTestSettings.FromEnvironment();

    public string ConnectionString { get; private set; } = "";

    /// <summary>Who each simulated user is and the project they work in, one of their own (research R15): every
    /// tenth works on the largest board, every tenth (offset by five) is the Project Admin of a project and also
    /// changes its team, and the others are Members of a project.</summary>
    public IReadOnlyList<Subject> Subjects { get; private set; } = [];

    /// <summary>Active users who are not Administrators, from whom Project Admins pick people to add.</summary>
    public IReadOnlyList<Guid> UserIds { get; private set; } = [];

    /// <summary>The project whose board shows about 500 cards.</summary>
    public string LargestProjectKey { get; private set; } = "";

    public int WorkItems { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (Settings.ConnectionString is { } connectionString)
        {
            ConnectionString = connectionString;
        }
        else
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "UpmsPerf" }.ConnectionString;
        }

        await using var db = AppDbContext.Create(ConnectionString);
        await db.Database.MigrateAsync();
        if (!await db.Projects.AnyAsync())
        {
            var options = SeedOptions.Baseline with { WorkItems = Settings.WorkItems };
            await new Seeder(ConnectionString, message => Console.WriteLine($"[seed] {message}")).RunAsync(options, CancellationToken.None);
        }

        var users = db.Users.AsNoTracking().Where(u => u.IsActive && u.OrganizationRole == OrganizationRole.User);
        UserIds = await users.OrderBy(u => u.UserName).Select(u => u.Id).ToListAsync();
        LargestProjectKey = await db.WorkItems.AsNoTracking()
            .GroupBy(w => w.ProjectId)
            .OrderByDescending(g => g.Count())
            .Select(g => db.Projects.Where(p => p.Id == g.Key).Select(p => p.Key).First())
            .FirstAsync();
        var contributions = await (
                from m in db.ProjectMembers.AsNoTracking()
                join u in users on m.UserId equals u.Id
                join p in db.Projects.AsNoTracking() on m.ProjectId equals p.Id
                where m.Role != ProjectRole.Viewer
                orderby u.UserName, p.Key
                select new Contribution(u.Id, p.Key, m.Role))
            .ToListAsync();
        Subjects = ChooseSubjects(contributions, LargestProjectKey, Math.Max(Settings.Users, 1));
        WorkItems = await db.WorkItems.IgnoreQueryFilters().CountAsync();
    }

    /// <summary>Different people as far as the data allows; each Project Admin subject has a project of their own.</summary>
    private static List<Subject> ChooseSubjects(List<Contribution> contributions, string largestProjectKey, int count)
    {
        var random = new Random(20260927);
        var onLargest = contributions.Where(c => c.ProjectKey == largestProjectKey).ToList();
        var admins = contributions
            .Where(c => c.Role == ProjectRole.ProjectAdmin && c.ProjectKey != largestProjectKey)
            .DistinctBy(c => c.ProjectKey)
            .ToList();
        var members = contributions
            .Where(c => c.Role == ProjectRole.Member && c.ProjectKey != largestProjectKey)
            .GroupBy(c => c.UserId)
            .Select(g => g.ElementAt(random.Next(g.Count())))
            .ToList();
        if (onLargest.Count == 0 || admins.Count == 0 || members.Count == 0)
        {
            throw new InvalidOperationException("The database has no teams to simulate. Seed it with tools/Upms.Seed.");
        }

        var subjects = new List<Subject>(count);
        var used = new HashSet<Guid>();
        int nextLargest = 0, nextAdmin = 0, nextMember = 0;
        for (var i = 0; i < count; i++)
        {
            var (pool, next) = (i % 10) switch
            {
                0 => (onLargest, nextLargest++),
                5 => (admins, nextAdmin++),
                _ => (members, nextMember++),
            };
            var chosen = pool[next % pool.Count];
            for (var tries = 0; tries < pool.Count && !used.Add(chosen.UserId); tries++)
            {
                chosen = pool[(next + tries + 1) % pool.Count];
            }

            subjects.Add(new Subject(chosen.UserId, chosen.ProjectKey, ChangesTeam: i % 10 == 5));
        }

        return subjects;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private sealed record Contribution(Guid UserId, string ProjectKey, ProjectRole Role);
}

/// <param name="ChangesTeam">A Project Admin who also adds and removes people (the "membership change" action).</param>
public sealed record Subject(Guid UserId, string ProjectKey, bool ChangesTeam);
