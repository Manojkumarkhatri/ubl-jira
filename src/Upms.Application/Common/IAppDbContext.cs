using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Common;

/// <summary>The application's unit of work over the single database (research R5). Each module
/// queries only its own sets; other modules' data is reached through their contracts.</summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<AuditEvent> AuditEvents { get; }

    DbSet<OrganizationSettings> OrganizationSettings { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectStatus> ProjectStatuses { get; }

    DbSet<WorkItem> WorkItems { get; }

    DbSet<WorkItemChange> WorkItemChanges { get; }

    DbSet<Comment> Comments { get; }

    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
