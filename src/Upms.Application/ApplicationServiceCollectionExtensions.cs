using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Application.Identity.Contracts;
using Upms.Application.Notifications;
using Upms.Application.Notifications.Contracts;
using Upms.Application.Projects;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work;
using Upms.Application.Work.Contracts;

namespace Upms.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers the application services of every module.</summary>
    public static IServiceCollection AddUpmsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<SetupOptions>(configuration.GetSection(SetupOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.PostConfigure<EmailOptions>(o => o.PublicBaseUrl = string.IsNullOrWhiteSpace(o.PublicBaseUrl)
            ? configuration["App:PublicBaseUrl"] ?? "" : o.PublicBaseUrl);

        services.AddScoped<ICallerContext, CallerContext>();
        services.AddScoped<IUserStatusReader, UserStatusReader>();
        services.AddScoped<TimeZoneResolver>();

        // Identity module
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IOrganizationSettingsReader, OrganizationSettingsReader>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
        services.AddScoped<IMembershipAuditLog, MembershipAuditLog>();
        services.AddScoped<IPlanningAuditLog, PlanningAuditLog>();
        services.AddScoped<IContentAuditLog, ContentAuditLog>();

        // Projects module
        services.AddScoped<IProjectAccess, ProjectAccess>();
        services.AddScoped<IProjectWorkflow, ProjectWorkflow>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ISpaceTrashService, SpaceTrashService>();
        services.AddScoped<IBoardColumnService, BoardColumnService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<IProjectTeam, ProjectTeam>();
        services.AddScoped<ISpaceNavigationService, SpaceNavigationService>();
        services.AddScoped<ISpaceViewService, SpaceViewService>();
        services.AddScoped<ISpaceAccessSync, SpaceAccessSync>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IPortfolioReads, PortfolioService>();

        // Work module
        services.AddScoped<IWorkItemCounts, WorkItemCounts>();
        services.AddScoped<IWorkItemStatusMover, WorkItemStatusMover>();
        services.AddScoped<IProjectPlanSetup, ProjectPlanSetup>();
        services.AddScoped<RankRebalancer>();
        services.AddScoped<IRankRebalancer>(sp => sp.GetRequiredService<RankRebalancer>());
        services.AddScoped<CardRanker>();
        services.AddScoped<AssigneeReads>();
        services.AddScoped<WorkItemReads>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<IWorkItemActionService, WorkItemActionService>();
        services.AddScoped<ISpaceFieldService, SpaceFieldService>();
        services.AddScoped<ICreateWorkService, CreateWorkService>();
        services.AddScoped<ISpaceSummaryService, SpaceSummaryService>();
        services.AddScoped<ISpaceReportService, SpaceReportService>();
        services.AddScoped<IMisService, MisService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IMyTasksService, MyTasksService>();
        services.AddScoped<IWorkItemListService, WorkItemListService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ISpaceDocService, SpaceDocService>();
        services.AddScoped<ISpaceFormService, SpaceFormService>();
        services.AddScoped<SprintReads>();
        services.AddScoped<IBacklogService, BacklogService>();
        services.AddScoped<ISprintService, SprintService>();
        services.AddScoped<StageLocks>();
        services.AddScoped<StageGateRules>();
        services.AddScoped<IStageGateService, StageGateService>();
        services.AddScoped<DependencyReads>();
        services.AddScoped<IDependencyService, DependencyService>();
        services.AddScoped<IWorkItemTitles, WorkItemTitles>();
        services.AddScoped<WatchList>();
        services.AddScoped<ItemEdits>();
        services.AddScoped<TextChecks>();
        services.AddScoped<StatusNotices>();
        services.AddScoped<IWatcherService, WatcherService>();
        services.AddScoped<IMentionService, MentionService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IAttachmentScanner, AttachmentScanner>();
        services.AddScoped<SearchIndexer>();
        services.AddScoped<ISearchIndexBackfill, SearchIndexer>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ISavedFilterService, SavedFilterService>();
        services.AddScoped<ILabelService, LabelService>();

        // Notifications module (Phase 4 research R1)
        services.AddScoped<INotificationCenter, NotificationCenter>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationCleanup, NotificationService>();
        services.AddScoped<IEmailDispatcher, EmailDispatcher>();
        return services;
    }
}
