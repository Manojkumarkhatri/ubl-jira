using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Projects;

/// <summary>Phase 1 rules of the single authorization point (research R7, contracts/permissions.md):
/// any active user may view and contribute; the owner and Administrators manage; only Administrators
/// restore. Role and ownership are read from the database on every call.</summary>
internal sealed class ProjectAccess(IAppDbContext db, ICallerContext caller) : IProjectAccess
{
    public async Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        var key = (projectKey ?? "").Trim().ToUpperInvariant();
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Key == key)
            .Select(p => new ProjectRef(p.Id, p.Key, p.OwnerId))
            .SingleOrDefaultAsync(ct);
        return project is null ? AppError.NotFound("project") : Evaluate(user, project, right);
    }

    public async Task<Result<ProjectAccessInfo>> RequireAsync(long projectId, ProjectRight right, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectRef(p.Id, p.Key, p.OwnerId))
            .SingleOrDefaultAsync(ct);
        return project is null ? AppError.NotFound("project") : Evaluate(user, project, right);
    }

    public async Task<bool> CanDeleteWorkItemAsync(long projectId, Guid workItemCreatorId, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return false;
        }

        if (user.IsAdministrator || user.UserId == workItemCreatorId)
        {
            return true;
        }

        return await db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId && p.OwnerId == user.UserId, ct);
    }

    private static Result<ProjectAccessInfo> Evaluate(CallerStatus user, ProjectRef project, ProjectRight right)
    {
        var canManage = user.IsAdministrator || project.OwnerId == user.UserId;
        var info = new ProjectAccessInfo(project.Id, project.Key, user.UserId, canManage, user.IsAdministrator);
        return right switch
        {
            ProjectRight.View or ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem => info,
            ProjectRight.Manage when canManage => info,
            ProjectRight.Manage => AppError.Forbidden("Only the project owner or an administrator can do that."),
            ProjectRight.Restore when user.IsAdministrator => info,
            ProjectRight.Restore => AppError.Forbidden("Only administrators can restore deleted tasks."),
            _ => AppError.Forbidden(),
        };
    }

    private sealed record ProjectRef(long Id, string Key, Guid OwnerId);
}
