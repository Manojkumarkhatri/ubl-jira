using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Infrastructure.Persistence;

/// <summary>The single database context (research R5): Identity tables, data protection keys and the
/// module tables, each configured in <c>Configurations/{Module}</c>.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<User, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<WorkItemChange> WorkItemChanges => Set<WorkItemChange>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>A context for tools and tests that run outside the web host.</summary>
    public static AppDbContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseUpmsSqlServer(connectionString).Options);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
