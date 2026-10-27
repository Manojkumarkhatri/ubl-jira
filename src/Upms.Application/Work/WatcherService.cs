using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

internal sealed class WatcherService(IAppDbContext db, IProjectAccess access, IProjectTeam team, WatchList watchers) : IWatcherService
{
    public async Task<Result<WatchersView>> GetAsync(string workItemKey, CancellationToken ct)
    {
        var (item, caller, error) = await RequireAsync(workItemKey, ProjectRight.View, ct);
        return error is not null ? error : await watchers.ViewAsync(item!.Id, caller!, ct);
    }

    public async Task<Result<WatchersView>> WatchAsync(string workItemKey, CancellationToken ct)
    {
        var (item, caller, error) = await RequireAsync(workItemKey, ProjectRight.View, ct);
        if (error is not null)
        {
            return error;
        }

        await watchers.AddAsync(item!.Id, [caller!.UserId], ct);
        await db.SaveChangesAsync(ct);
        return await watchers.ViewAsync(item.Id, caller, ct);
    }

    public async Task<Result<WatchersView>> StopWatchingAsync(string workItemKey, CancellationToken ct)
    {
        var (item, caller, error) = await RequireAsync(workItemKey, ProjectRight.View, ct);
        if (error is not null)
        {
            return error;
        }

        await StopAsync(item!.Id, caller!.UserId, ct);
        return await watchers.ViewAsync(item.Id, caller, ct);
    }

    public async Task<Result<WatchersView>> AddAsync(string workItemKey, Guid userId, CancellationToken ct)
    {
        var (item, caller, error) = await RequireAsync(workItemKey, ProjectRight.Manage, ct);
        if (error is not null)
        {
            return error;
        }

        if (!(await team.GetMembersAsync(item!.ProjectId, ct)).Any(m => m.UserId == userId && m.IsActive))
        {
            return AppError.Rule(ErrorCodes.NotAMember, "Only active members of the space can be added as watchers.");
        }

        await watchers.AddAsync(item.Id, [userId], ct);
        await db.SaveChangesAsync(ct);
        return await watchers.ViewAsync(item.Id, caller!, ct);
    }

    public async Task<Result<WatchersView>> RemoveAsync(string workItemKey, Guid userId, CancellationToken ct)
    {
        var (item, caller, error) = await RequireAsync(workItemKey, ProjectRight.Manage, ct);
        if (error is not null)
        {
            return error;
        }

        await StopAsync(item!.Id, userId, ct);
        return await watchers.ViewAsync(item.Id, caller!, ct);
    }

    private Task<int> StopAsync(long workItemId, Guid userId, CancellationToken ct) =>
        db.Watches.Where(w => w.WorkItemId == workItemId && w.UserId == userId).ExecuteDeleteAsync(ct);

    private async Task<(WorkItem? Item, ProjectAccessInfo? Caller, AppError? Error)> RequireAsync(string workItemKey,
        ProjectRight right, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return (null, null, AppError.NotFound("task"));
        }

        var allowed = await access.RequireAsync(item.ProjectId, right, ct);
        return allowed.IsSuccess ? (item, allowed.Value, null) : (null, null, allowed.Error);
    }
}
