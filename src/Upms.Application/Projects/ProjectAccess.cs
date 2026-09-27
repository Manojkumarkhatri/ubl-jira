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

        var role = await RoleAsync(projectId, user.UserId, ct);
        return role == ProjectRole.ProjectAdmin || (role == ProjectRole.Member && user.UserId == workItemCreatorId);
    }

    /// <summary>The project with the caller's role in it, read in one query.</summary>
    private IQueryable<ProjectWithRole> WithRoleOf(IQueryable<Project> projects, Guid userId) =>
        projects.AsNoTracking().Select(p => new ProjectWithRole(p.Id, p.Key,
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
            return AppError.NotFound("project");
        }

        if (project.Role is null && !user.IsAdministrator)
        {
            // Non-members learn nothing: the answer is the one for a project that does not exist (FR-002).
            LogDenied(logger, user.UserId, right, project.Key);
            return AppError.NotFound("project");
        }

        var canContribute = user.IsAdministrator || project.Role is ProjectRole.ProjectAdmin or ProjectRole.Member;
        var canManage = user.IsAdministrator || project.Role == ProjectRole.ProjectAdmin;
        var info = new ProjectAccessInfo(project.Id, project.Key, user.UserId, project.Role, canContribute, canManage,
            user.IsAdministrator);
        return right switch
        {
            ProjectRight.View => info,
            ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem when canContribute => info,
            ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem =>
                Denied(user.UserId, right, project.Key, "Viewers can see this project but not change it."),
            ProjectRight.Manage when canManage => info,
            ProjectRight.Manage => Denied(user.UserId, right, project.Key, "Only a Project Admin or an administrator can do that."),
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

    private sealed record ProjectWithRole(long Id, string Key, ProjectRole? Role);
}
