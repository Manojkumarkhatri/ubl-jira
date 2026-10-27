using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>An item's watchers for the Work services (Phase 4 research R7): who watches, adding people, and the list as
/// the drawer shows it, leaving out anyone who can no longer see the item (FR-014).</summary>
internal sealed class WatchList(IAppDbContext db, IProjectTeam team, IUserDirectory users, TimeProvider time)
{
    /// <summary>Adds the people who are not watching yet to the caller's unit of work.</summary>
    public async Task AddAsync(long workItemId, IReadOnlyCollection<Guid> people, CancellationToken ct)
    {
        var ids = people.Distinct().ToList();
        var watching = await db.Watches.AsNoTracking()
            .Where(w => w.WorkItemId == workItemId && ids.Contains(w.UserId))
            .Select(w => w.UserId)
            .ToListAsync(ct);
        watching.AddRange(db.Watches.Local.Where(w => w.WorkItemId == workItemId).Select(w => w.UserId));
        foreach (var userId in ids.Except(watching))
        {
            db.Watches.Add(Watch.Start(workItemId, userId, time.GetUtcNow()));
        }
    }

    public async Task<IReadOnlyList<Guid>> OfAsync(long workItemId, CancellationToken ct) =>
        await db.Watches.AsNoTracking().Where(w => w.WorkItemId == workItemId).Select(w => w.UserId).ToListAsync(ct);

    /// <summary>The watchers of several items in one read.</summary>
    public async Task<ILookup<long, Guid>> OfAsync(IReadOnlyCollection<long> workItemIds, CancellationToken ct)
    {
        var ids = workItemIds.Distinct().ToList();
        return (await db.Watches.AsNoTracking()
            .Where(w => ids.Contains(w.WorkItemId))
            .Select(w => new { w.WorkItemId, w.UserId })
            .ToListAsync(ct)).ToLookup(w => w.WorkItemId, w => w.UserId);
    }

    public async Task<WatchersView> ViewAsync(long workItemId, ProjectAccessInfo caller, CancellationToken ct)
    {
        var seeing = await team.WhoCanSeeAsync(caller.ProjectId, await OfAsync(workItemId, ct), ct);
        var people = (await users.GetAsync(seeing.ToList(), ct)).Values
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id)
            .Select(p => new PersonView(p.Id, p.DisplayName, p.UserName))
            .ToList();
        return new WatchersView(people.Count, seeing.Contains(caller.UserId), people, caller.CanManage);
    }
}
