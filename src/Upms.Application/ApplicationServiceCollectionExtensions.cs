using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upms.Application.Common;
using Upms.Application.Identity;
using Upms.Application.Identity.Contracts;
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

        services.AddScoped<ICallerContext, CallerContext>();
        services.AddScoped<IUserStatusReader, UserStatusReader>();
        services.AddScoped<TimeZoneResolver>();

        // Identity module
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IOrganizationSettingsReader, OrganizationSettingsReader>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IMembershipAuditLog, MembershipAuditLog>();

        // Projects module
        services.AddScoped<IProjectAccess, ProjectAccess>();
        services.AddScoped<IProjectWorkflow, ProjectWorkflow>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IBoardColumnService, BoardColumnService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<IProjectTeam, ProjectTeam>();

        // Work module
        services.AddScoped<IWorkItemCounts, WorkItemCounts>();
        services.AddScoped<IWorkItemStatusMover, WorkItemStatusMover>();
        services.AddScoped<RankRebalancer>();
        services.AddScoped<IRankRebalancer>(sp => sp.GetRequiredService<RankRebalancer>());
        services.AddScoped<CardRanker>();
        services.AddScoped<AssigneeReads>();
        services.AddScoped<WorkItemReads>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IMyTasksService, MyTasksService>();
        return services;
    }
}
