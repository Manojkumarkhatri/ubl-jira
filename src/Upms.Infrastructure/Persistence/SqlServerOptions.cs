using Microsoft.EntityFrameworkCore;

namespace Upms.Infrastructure.Persistence;

internal static class SqlServerOptions
{
    /// <summary>SQL Server with migrations kept in this assembly. Services use explicit transactions, so
    /// the retrying execution strategy is not enabled (add it with care if the database moves to Azure SQL).</summary>
    public static DbContextOptionsBuilder UseUpmsSqlServer(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name));

    public static DbContextOptionsBuilder<TContext> UseUpmsSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> options, string connectionString)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)options).UseUpmsSqlServer(connectionString);
        return options;
    }
}
