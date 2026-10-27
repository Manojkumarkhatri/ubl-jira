using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;
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

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<UserTypeRights> UserTypeRights => Set<UserTypeRights>();

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<PortfolioLead> PortfolioLeads => Set<PortfolioLead>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<StarredProject> StarredProjects => Set<StarredProject>();

    public DbSet<SpaceShortcut> SpaceShortcuts => Set<SpaceShortcut>();

    public DbSet<SpaceDoc> SpaceDocs => Set<SpaceDoc>();

    public DbSet<SpaceForm> SpaceForms => Set<SpaceForm>();

    public DbSet<ProjectVisit> ProjectVisits => Set<ProjectVisit>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<WorkItemChange> WorkItemChanges => Set<WorkItemChange>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<SprintScopeEvent> SprintScopeEvents => Set<SprintScopeEvent>();

    public DbSet<StageGate> StageGates => Set<StageGate>();

    public DbSet<GateApprover> GateApprovers => Set<GateApprover>();

    public DbSet<GateRequest> GateRequests => Set<GateRequest>();

    public DbSet<GateDecision> GateDecisions => Set<GateDecision>();

    public DbSet<Dependency> Dependencies => Set<Dependency>();

    public DbSet<Watch> Watches => Set<Watch>();

    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();

    public DbSet<WorkItemVote> WorkItemVotes => Set<WorkItemVote>();

    public DbSet<SpaceField> SpaceFields => Set<SpaceField>();

    public DbSet<FieldValue> FieldValues => Set<FieldValue>();

    public DbSet<WorkTypeLayout> WorkTypeLayouts => Set<WorkTypeLayout>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<SearchTerm> SearchTerms => Set<SearchTerm>();

    public DbSet<SearchIndexProgress> SearchIndexProgress => Set<SearchIndexProgress>();

    public DbSet<RecentView> RecentViews => Set<RecentView>();

    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();

    /// <summary>The files' bytes; written and read as streams by <c>AttachmentContentStore</c>, never loaded whole.</summary>
    public DbSet<AttachmentContent> AttachmentContents => Set<AttachmentContent>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

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
