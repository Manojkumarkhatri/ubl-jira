using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;

namespace Upms.Application.Projects;

/// <summary>Phase 1 rules of the single authorization point (research R7, contracts/permissions.md):
/// any active user may view and contribute; the owner and Administrators manage; only Administrators
/// restore. Role and ownership are read from the database on every call.</summary>
internal sealed partial class ProjectAccess(IAppDbContext db, ICallerContext caller, ILogger<ProjectAccess> logger) : IProjectAccess
{
    public async Task<Result<ProjectAccessInfo>> RequireAsync(string projectKey, ProjectRight right, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return Denied(null, right, projectKey);
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
            return Denied(null, right, projectId.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

    private Result<ProjectAccessInfo> Evaluate(CallerStatus user, ProjectRef project, ProjectRight right)
    {
        var canManage = user.IsAdministrator || project.OwnerId == user.UserId;
        var info = new ProjectAccessInfo(project.Id, project.Key, user.UserId, canManage, user.IsAdministrator);
        return right switch
        {
            ProjectRight.View or ProjectRight.Contribute or ProjectRight.DeleteOwnWorkItem => info,
            ProjectRight.Manage when canManage => info,
            ProjectRight.Manage => Denied(user.UserId, right, project.Key, "Only the project owner or an administrator can do that."),
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

    private sealed record ProjectRef(long Id, string Key, Guid OwnerId);
}
