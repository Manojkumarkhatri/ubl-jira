using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;
using Upms.Infrastructure.Identity;
using Upms.Infrastructure.Persistence;

namespace Upms.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

    /// <summary>Registers persistence, ASP.NET Core Identity and the audit log.</summary>
    public static IServiceCollection AddUpmsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"The connection string 'ConnectionStrings:{ConnectionStringName}' is not configured.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddDbContext<AppDbContext>(options => options.UseUpmsSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<User>(IdentitySetup.Configure)
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<AuditingSignInManager>()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<UsernamePasswordValidator>()
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddUserValidator<UserRulesValidator>()
            .AddClaimsPrincipalFactory<UpmsClaimsPrincipalFactory>();

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<Upms.Application.Projects.Contracts.IWorkItemNumberAllocator, WorkItemNumberAllocator>();
        services.AddHostedService<Workers.RankRebalanceWorker>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);
        return services;
    }

    /// <summary>Applies pending migrations (used by tests and, when enabled, at startup).</summary>
    public static async Task MigrateUpmsDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }
}
