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
        services.AddScoped<IAdministratorLock, AdministratorLock>();
        services.AddScoped<Upms.Application.Work.IPlanLock, PlanLock>();
        services.AddScoped<Upms.Application.Projects.Contracts.IWorkItemNumberAllocator, WorkItemNumberAllocator>();
        services.AddHostedService<Workers.RankRebalanceWorker>();
        services.AddHostedService<Workers.NotificationCleanupWorker>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();

        var health = services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        // Attachments (Phase 4 research R12–R14): bytes in the database, checked by ClamAV when it is configured. Without
        // a scanner every upload stays "being checked": the check fails closed. The scanner only degrades readiness.
        services.Configure<Malware.ClamAvOptions>(configuration.GetSection(Malware.ClamAvOptions.SectionName));
        services.AddScoped<Upms.Application.Work.IAttachmentContentStore, AttachmentContentStore>();
        if (string.IsNullOrWhiteSpace(configuration[$"{Malware.ClamAvOptions.SectionName}:Host"]))
        {
            services.AddSingleton<Upms.Application.Work.IMalwareScanner, Malware.NoMalwareScanner>();
        }
        else
        {
            services.AddSingleton<Malware.ClamAvScanner>();
            services.AddSingleton<Upms.Application.Work.IMalwareScanner>(sp => sp.GetRequiredService<Malware.ClamAvScanner>());
            health.Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration("malware-scanner",
                sp => sp.GetRequiredService<Malware.ClamAvScanner>(),
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, ["ready"]));
        }

        services.AddHostedService<Workers.AttachmentScanWorker>();
        services.AddHostedService<Workers.SearchIndexWorker>();

        // Email (Phase 4 research R10): an outbox sent by a worker through the company mail server; off without a host, and
        // a mail server that cannot be reached only degrades readiness.
        services.Configure<Email.SmtpOptions>(configuration.GetSection(Email.SmtpOptions.SectionName));
        services.AddSingleton<Email.MailKitEmailTransport>();
        services.AddSingleton<Upms.Application.Notifications.IEmailTransport>(sp => sp.GetRequiredService<Email.MailKitEmailTransport>());
        if (!string.IsNullOrWhiteSpace(configuration[$"{Email.SmtpOptions.SectionName}:Host"]))
        {
            health.Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration("mail-server",
                sp => sp.GetRequiredService<Email.MailKitEmailTransport>(),
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, ["ready"]));
        }

        services.AddHostedService<Workers.EmailDispatchWorker>();
        return services;
    }

    /// <summary>Applies pending migrations (used by tests and, when enabled, at startup).</summary>
    public static async Task MigrateUpmsDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }
}
