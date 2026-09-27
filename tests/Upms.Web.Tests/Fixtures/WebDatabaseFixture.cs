using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Upms.Web.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(WebDatabaseFixture))]

namespace Upms.Web.Tests.Fixtures;

/// <summary>A real SQL Server for host-level tests; each test class gets its own database.</summary>
public sealed class WebDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private int _databaseCounter;

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    /// <summary>A connection string for a new, empty database; the app migrates it on startup.</summary>
    public string NewDatabase() => new SqlConnectionStringBuilder(_container.GetConnectionString())
    {
        InitialCatalog = $"UpmsWeb{Interlocked.Increment(ref _databaseCounter)}",
    }.ConnectionString;

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
