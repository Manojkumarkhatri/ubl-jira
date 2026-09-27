using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Upms.Application.Tests.Fixtures;
using Upms.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Upms.Application.Tests.Fixtures;

/// <summary>One real SQL Server for the whole test run (constitution II): migrations are applied once
/// and every test starts from an empty database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private Respawner? _respawner;

    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "UpmsTests",
        }.ConnectionString;

        await using (var db = AppDbContext.Create(ConnectionString))
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            WithReseed = true,
            // Append-only tables are truncated separately (their triggers refuse DELETE); the settings
            // row is kept and reset.
            TablesToIgnore =
            [
                new Table("dbo", "__EFMigrationsHistory"),
                new Table("dbo", "OrganizationSettings"),
                new Table("dbo", "AuditEvents"),
                new Table("dbo", "WorkItemChanges"),
            ],
        });
    }

    /// <summary>Empties every table except the migration history and resets the settings row.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                TRUNCATE TABLE [dbo].[AuditEvents];
                IF OBJECT_ID(N'[dbo].[WorkItemChanges]') IS NOT NULL TRUNCATE TABLE [dbo].[WorkItemChanges];
                UPDATE [dbo].[OrganizationSettings] SET [SetupCompletedAt] = NULL;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await _respawner!.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
