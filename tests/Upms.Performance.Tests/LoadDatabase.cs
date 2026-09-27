using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Upms.Domain.Identity;
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

    /// <summary>Active users who are not Administrators, to act as the simulated users.</summary>
    public IReadOnlyList<Guid> UserIds { get; private set; } = [];

    public IReadOnlyList<string> ProjectKeys { get; private set; } = [];

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

        UserIds = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.OrganizationRole == OrganizationRole.User)
            .OrderBy(u => u.UserName)
            .Select(u => u.Id)
            .Take(Math.Max(Settings.Users, 1))
            .ToListAsync();
        ProjectKeys = await db.Projects.AsNoTracking().OrderBy(p => p.Key).Select(p => p.Key).ToListAsync();
        LargestProjectKey = await db.WorkItems.AsNoTracking()
            .GroupBy(w => w.ProjectId)
            .OrderByDescending(g => g.Count())
            .Select(g => db.Projects.Where(p => p.Id == g.Key).Select(p => p.Key).First())
            .FirstAsync();
        WorkItems = await db.WorkItems.IgnoreQueryFilters().CountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
