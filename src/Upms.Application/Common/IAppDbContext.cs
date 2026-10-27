using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;
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

    DbSet<Department> Departments { get; }

    DbSet<UserTypeRights> UserTypeRights { get; }

    DbSet<Portfolio> Portfolios { get; }

    DbSet<PortfolioLead> PortfolioLeads { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectStatus> ProjectStatuses { get; }

    DbSet<ProjectMember> ProjectMembers { get; }

    DbSet<StarredProject> StarredProjects { get; }

    DbSet<SpaceShortcut> SpaceShortcuts { get; }

    DbSet<SpaceDoc> SpaceDocs { get; }

    DbSet<SpaceForm> SpaceForms { get; }

    DbSet<ProjectVisit> ProjectVisits { get; }

    DbSet<WorkItem> WorkItems { get; }

    DbSet<WorkItemChange> WorkItemChanges { get; }

    DbSet<Comment> Comments { get; }

    DbSet<Sprint> Sprints { get; }

    DbSet<SprintScopeEvent> SprintScopeEvents { get; }

    DbSet<StageGate> StageGates { get; }

    DbSet<GateApprover> GateApprovers { get; }

    DbSet<GateRequest> GateRequests { get; }

    DbSet<GateDecision> GateDecisions { get; }

    DbSet<Dependency> Dependencies { get; }

    DbSet<Watch> Watches { get; }

    DbSet<WorkLog> WorkLogs { get; }

    DbSet<WorkItemVote> WorkItemVotes { get; }

    DbSet<SpaceField> SpaceFields { get; }

    DbSet<FieldValue> FieldValues { get; }

    DbSet<WorkTypeLayout> WorkTypeLayouts { get; }

    DbSet<Attachment> Attachments { get; }

    DbSet<SearchTerm> SearchTerms { get; }

    DbSet<SearchIndexProgress> SearchIndexProgress { get; }

    DbSet<RecentView> RecentViews { get; }

    DbSet<SavedFilter> SavedFilters { get; }

    DbSet<Label> Labels { get; }

    DbSet<WorkItemLabel> WorkItemLabels { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<EmailMessage> EmailMessages { get; }

    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
