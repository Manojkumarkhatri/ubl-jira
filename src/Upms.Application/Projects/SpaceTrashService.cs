using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Projects;

/// <summary>The space trash (<see cref="ISpaceTrashService"/>). Nothing is erased: the space only gets a deleted mark, so its
/// history and audit stay, and its key and name stay taken.</summary>
internal sealed class SpaceTrashService(
    IAppDbContext db,
    IProjectAccess access,
    ICallerContext caller,
    IUserDirectory users,
    IAuditLog auditLog,
    ISpaceAccessSync spaceAccess,
    TimeProvider time) : ISpaceTrashService
{
    public async Task<bool> CanDeleteAsync(string projectKey, CancellationToken ct) =>
        (await AllowedAsync(projectKey, ct)).IsSuccess;

    public async Task<Result> DeleteAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await AllowedAsync(projectKey, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value!.ProjectId, ct);
        project.MoveToTrash(allowed.Value!.UserId, time.GetUtcNow());
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await auditLog.WriteAsync(AuditEventType.SpaceMovedToTrash, null, project.Key, new { project.Name }, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<TrashedSpace>>> ListAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true, IsAdministrator: true })
        {
            return AppError.Forbidden("Only Admins see the trash.");
        }

        var rows = await db.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .Select(p => new
            {
                p.Key,
                p.Name,
                p.DeletedAt,
                p.DeletedById,
                Items = db.WorkItems.Count(w => w.ProjectId == p.Id),
            })
            .ToListAsync(ct);
        var names = await users.GetAsync(rows.Where(r => r.DeletedById is not null).Select(r => r.DeletedById!.Value).Distinct().ToList(), ct);
        return rows.ConvertAll(r => new TrashedSpace(r.Key, r.Name, r.DeletedAt ?? DateTimeOffset.MinValue,
            r.DeletedById is { } by && names.TryGetValue(by, out var who) ? who.DisplayName : "Unknown user", r.Items));
    }

    public async Task<Result> RestoreAsync(string projectKey, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true, IsAdministrator: true })
        {
            return AppError.Forbidden("Only Admins restore spaces.");
        }

        var key = (projectKey ?? "").Trim().ToUpperInvariant();
        var project = await db.Projects.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Key == key && p.IsDeleted, ct);
        if (project is null)
        {
            return AppError.NotFound("space");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        project.Restore(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await auditLog.WriteAsync(AuditEventType.SpaceRestored, null, project.Key, new { project.Name }, ct);
        // Its portfolio's leads and Heads may have changed while it was in the trash.
        await spaceAccess.SyncSpacesAsync([project.Id], ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    /// <summary>An Admin anywhere; with the tick, a Head in their own department and anyone who manages the space.</summary>
    private async Task<Result<Contracts.ProjectAccessInfo>> AllowedAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var info = allowed.Value!;
        if (info.IsAdministrator)
        {
            return info;
        }

        if (await caller.GetAsync(ct) is not { IsActive: true } me || !me.Can(Rights.DeleteSpaces))
        {
            return AppError.Forbidden("You don't have the \"Delete spaces\" right. Ask an Admin.");
        }

        if (info.CanManage)
        {
            return info;
        }

        var department = await db.Projects.AsNoTracking()
            .Where(p => p.Id == info.ProjectId && p.PortfolioId != null)
            .Select(p => db.Portfolios.Where(f => f.Id == p.PortfolioId).Select(f => (long?)f.DepartmentId).FirstOrDefault())
            .FirstOrDefaultAsync(ct);
        return me.Type == UserType.Head && department is not null && department == me.DepartmentId
            ? info
            : AppError.Forbidden("Only an Admin, the Head of its department or someone who manages this space can delete it.");
    }
}
