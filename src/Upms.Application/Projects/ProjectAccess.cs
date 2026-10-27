using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Phase 2 rules of the single authorization point (Phase 2 research R2, contracts/permissions.md): members
/// view; Members and Project Admins contribute; Project Admins manage; administrators may do everything in every
/// project; only administrators restore. Anyone else is told the project does not exist. The caller's status and
/// role are read from the database on every call, so removals and role changes apply at once (FR-011).</summary>
internal sealed partial class ProjectAccess(IAppDbContext db, ICallerContext caller, ILogger<ProjectAccess> logger) : IProjectAccess
{
    public async Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return Denied(null, right, projectKey);
        }

        var key = (projectKey ?? "").Trim().ToUpperInvariant();
        var project = await WithRoleOf(db.Projects.Where(p => p.Key == key), user.UserId).SingleOrDefaultAsync(ct);
        return Evaluate(user, project, right);
    }

    public async Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectRight right, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return Denied(null, right, projectId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var project = await WithRoleOf(db.Projects.Where(p => p.Id == projectId), user.UserId).SingleOrDefaultAsync(ct);
        return Evaluate(user, project, right);
    }

    public async Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return false;
        }

        if (user.IsAdministrator)
        {
            return true;
        }

        // The ticks decide (owner, 2026-10-26): "Delete any work item", or "Delete work items they created" for their own,
        // in a space where they can edit.
        var role = await RoleAsync(projectId, user.UserId, ct);
        if (role is not (ProjectRole.ProjectAdmin or ProjectRole.Member))
        {
            return false;
        }

        return user.Can(Upms.Domain.Common.Rights.DeleteAnyWork)
            || (user.Can(Upms.Domain.Common.Rights.DeleteOwnWork) && user.UserId == workItemCreatorId);
    }

    public async Task<IReadOnlySet<long>?> VisibleProjectIdsAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return new HashSet<long>();
        }

        if (user.IsAdministrator)
        {
            // Every space, unless some are in the trash.
            return await db.Projects.IgnoreQueryFilters().AnyAsync(p => p.IsDeleted, ct)
                ? (await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct)).ToHashSet()
                : null;
        }

        return (await MemberOf(user.UserId).ToListAsync(ct)).ToHashSet();
    }

    /// <summary>The spaces a person is in, leaving out those in the trash.</summary>
    private IQueryable<long> MemberOf(Guid userId) =>
        db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && db.Projects.Any(p => p.Id == m.ProjectId))
            .Select(m => m.ProjectId);

    public async Task<IReadOnlyList<ContributableSpace>> ContributableAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return [];
        }

        var spaces = user.IsAdministrator
            ? db.Projects.AsNoTracking().Select(p => new ContributableSpace(p.Id, p.Key, p.Name, p.Template, p.WorkTypes, true))
            : db.ProjectMembers.AsNoTracking()
                .Where(m => m.UserId == user.UserId && (m.Role == ProjectRole.ProjectAdmin || m.Role == ProjectRole.Member))
                .Join(db.Projects, m => m.ProjectId, p => p.Id,
                    (m, p) => new ContributableSpace(p.Id, p.Key, p.Name, p.Template, p.WorkTypes, m.Role == ProjectRole.ProjectAdmin));
        return (await spaces.ToListAsync(ct)).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlySet<long>> MemberProjectIdsAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return new HashSet<long>();
        }

        return (await MemberOf(user.UserId).ToListAsync(ct)).ToHashSet();
    }

    /// <summary>The project with the caller's role in it, read in one query.</summary>
    private IQueryable<ProjectWithRole> WithRoleOf(IQueryable<Project> projects, Guid userId) =>
        projects.AsNoTracking().Select(p => new ProjectWithRole(p.Id, p.Key, p.Template, p.WorkTypes,
            db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == userId).Select(m => (ProjectRole?)m.Role).FirstOrDefault()));

    private Task<ProjectRole?> RoleAsync(long projectId, Guid userId, CancellationToken ct) =>
        db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    private Result<ProjectAccessInfo> Evaluate(CallerStatus user, ProjectWithRole? project, ProjectRight right)
    {
        if (project is null)
        {
            return AppError.NotFound("space");
        }

        if (project.Role is null && !user.IsAdministrator)
        {
            // Non-members learn nothing: the answer is the one for a project that does not exist (FR-002).
            LogDenied(logger, user.UserId, right, project.Key);
            return AppError.NotFound("space");
        }

        var canContribute = user.IsAdministrator || project.Role is ProjectRole.ProjectAdmin or ProjectRole.Member;
        var canManage = user.IsAdministrator || project.Role == ProjectRole.ProjectAdmin;
        var info = new ProjectAccessInfo(project.Id, project.Key, user.UserId, project.Role, canContribute, canManage,
            user.IsAdministrator, project.Template, project.WorkTypes);
        return right switch
        {
            ProjectRight.View => info,
            ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem when canContribute => info,
            ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem =>
                Denied(user.UserId, right, project.Key, "Viewers can see this space but not change it."),
            ProjectRight.Manage when canManage => info,
            ProjectRight.Manage => Denied(user.UserId, right, project.Key, "Only someone who manages this space, or an Admin, can do that."),
            ProjectRight.Restore when user.IsAdministrator => info,
            ProjectRight.Restore => Denied(user.UserId, right, project.Key, "Only administrators can restore deleted tasks."),
            _ => Denied(user.UserId, right, project.Key),
        };
    }

    // Refusals are logged for monitoring (OWASP ASVS 7.2.2); the log holds IDs, never project content.
    private AppError Denied(Guid? userId, ProjectRight right, string? project, string? message = null)
    {
        LogDenied(logger, userId, right, project);
        return message is null ? AppError.Forbidden() : AppError.Forbidden(message);
    }

    [LoggerMessage(EventId = 4030, Level = LogLevel.Warning, Message = "Access denied: user {UserId} asked for {Right} on project {Project}")]
    private static partial void LogDenied(ILogger logger, Guid? userId, ProjectRight right, string? project);

    private sealed record ProjectWithRole(long Id, string Key, Upms.Domain.Common.ProjectTemplate Template,
        Upms.Domain.Common.WorkTypeSet WorkTypes, ProjectRole? Role);
}
