using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Stars and visits (Jira parity pass FR-021, FR-021b; research R6, R7). Rows of spaces the caller can no longer see
/// are kept but never shown, so removing someone from a space needs no cleanup.</summary>
internal sealed class SpaceNavigationService(IAppDbContext db, IProjectAccess access, ICallerContext caller, TimeProvider time)
    : ISpaceNavigationService
{
    public async Task<Result<SidebarView>> GetSidebarAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        var visible = await access.VisibleProjectIdsAsync(ct);
        var starred = (await StarredOfAsync(user.UserId, ct)).Where(s => visible is null || visible.Contains(s.Id)).ToList();
        var starredIds = starred.Select(s => s.Id).ToHashSet();
        var visited = (await (
                from visit in db.ProjectVisits.AsNoTracking()
                where visit.UserId == user.UserId
                join project in db.Projects.AsNoTracking() on visit.ProjectId equals project.Id
                orderby visit.VisitedAt descending
                select new { project.Id, project.Key, project.Name, project.Template })
            .ToListAsync(ct))
            .Where(v => visible is null || visible.Contains(v.Id))
            .Select(v => (v.Id, Link: new SpaceLink(v.Key, v.Name, v.Template, starredIds.Contains(v.Id))))
            .ToList();
        var spaces = starred.Select(s => s.Link)
            .Concat(visited.Where(v => !starredIds.Contains(v.Id)).Select(v => v.Link))
            .Take(ISpaceNavigationService.SidebarSpaces)
            .ToList();
        return new SidebarView(
            starred.Select(s => s.Link).ToList(),
            visited.Take(ISpaceNavigationService.RecentSpaces).Select(v => v.Link).ToList(),
            spaces,
            user.IsAdministrator);
    }

    public async Task<Result> StarAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var (userId, projectId) = (allowed.Value!.UserId, allowed.Value.ProjectId);
        if (await db.StarredProjects.AnyAsync(s => s.UserId == userId && s.ProjectId == projectId, ct))
        {
            return Result.Ok();
        }

        var star = StarredProject.Of(userId, projectId, time.GetUtcNow());
        db.StarredProjects.Add(star);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(star).State = EntityState.Detached; // starred in two windows at once: one star is enough
        }

        return Result.Ok();
    }

    public async Task<Result> UnstarAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var (userId, projectId) = (allowed.Value!.UserId, allowed.Value.ProjectId);
        await db.StarredProjects.Where(s => s.UserId == userId && s.ProjectId == projectId).ExecuteDeleteAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RecordVisitAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var (userId, projectId) = (allowed.Value!.UserId, allowed.Value.ProjectId);
        var now = time.GetUtcNow();
        if (await db.ProjectVisits.Where(v => v.UserId == userId && v.ProjectId == projectId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.VisitedAt, now), ct) > 0)
        {
            return Result.Ok();
        }

        var visit = ProjectVisit.Of(userId, projectId, now);
        db.ProjectVisits.Add(visit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(visit).State = EntityState.Detached; // opened in two windows at once: one visit is enough
            return Result.Ok();
        }

        var oldestKept = await db.ProjectVisits.Where(v => v.UserId == userId)
            .OrderByDescending(v => v.VisitedAt)
            .Skip(ProjectVisit.Kept - 1)
            .Select(v => (DateTimeOffset?)v.VisitedAt)
            .FirstOrDefaultAsync(ct);
        if (oldestKept is { } kept)
        {
            await db.ProjectVisits.Where(v => v.UserId == userId && v.VisitedAt < kept).ExecuteDeleteAsync(ct);
        }

        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<SpaceLink>>> StarredAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        var visible = await access.VisibleProjectIdsAsync(ct);
        IReadOnlyList<SpaceLink> starred = (await StarredOfAsync(user.UserId, ct))
            .Where(s => visible is null || visible.Contains(s.Id))
            .Select(s => s.Link)
            .ToList();
        return Result<IReadOnlyList<SpaceLink>>.Ok(starred);
    }

    private async Task<List<(long Id, SpaceLink Link)>> StarredOfAsync(Guid userId, CancellationToken ct) =>
        (await (
                from star in db.StarredProjects.AsNoTracking()
                where star.UserId == userId
                join project in db.Projects.AsNoTracking() on star.ProjectId equals project.Id
                select new { project.Id, project.Key, project.Name, project.Template })
            .ToListAsync(ct))
        .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
        .Select(s => (s.Id, new SpaceLink(s.Key, s.Name, s.Template, true)))
        .ToList();
}
