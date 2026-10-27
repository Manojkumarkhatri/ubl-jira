using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Space access from types, ticks and portfolios (<see cref="ISpaceAccessSync"/>): roles follow
/// <see cref="ProjectRoleNames.For"/>; a portfolio gives its active leads, and its department its active Heads, the role
/// their ticks give, marked as from the portfolio so it goes when they stop leading it.</summary>
internal sealed class SpaceAccessSync(IAppDbContext db, IUserDirectory users, TimeProvider time) : ISpaceAccessSync
{
    public async Task SyncPeopleAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var ids = userIds.Distinct().ToList();
        var people = await users.AccessOfAsync(ids, ct);
        var departmentIds = people.Values.Where(p => p.DepartmentId is not null).Select(p => p.DepartmentId!.Value).Distinct().ToList();
        // The spaces they are in, and their departments' spaces (a Head's access, given or taken away).
        var projectIds = await db.ProjectMembers.Where(m => ids.Contains(m.UserId)).Select(m => m.ProjectId)
            .Union(db.Projects.Where(p => p.PortfolioId != null
                    && db.Portfolios.Any(f => f.Id == p.PortfolioId && departmentIds.Contains(f.DepartmentId)))
                .Select(p => p.Id))
            .Distinct()
            .ToListAsync(ct);
        var projects = await db.Projects.Include(p => p.Members).Where(p => projectIds.Contains(p.Id)).ToListAsync(ct);
        var now = time.GetUtcNow();
        foreach (var project in projects)
        {
            foreach (var person in people.Values)
            {
                project.SetRoleFromRights(person.UserId, ProjectRoleNames.For(person.Rights), now);
            }
        }

        await GrantAsync(projects, now, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SyncSpacesAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        var ids = projectIds.Distinct().ToList();
        var projects = await db.Projects.Include(p => p.Members).Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        await GrantAsync(projects, time.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Gives each space's portfolio leads and department Heads their access, and takes back what is no longer given.</summary>
    private async Task GrantAsync(List<Project> projects, DateTimeOffset now, CancellationToken ct)
    {
        var portfolioIds = projects.Where(p => p.PortfolioId is not null).Select(p => p.PortfolioId!.Value).Distinct().ToList();
        var departments = await db.Portfolios.AsNoTracking().Where(f => portfolioIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.DepartmentId, ct);
        var leads = await db.PortfolioLeads.AsNoTracking().Where(l => portfolioIds.Contains(l.PortfolioId)).ToListAsync(ct);
        var leadAccess = await users.AccessOfAsync(leads.Select(l => l.UserId).ToList(), ct);
        var heads = await users.HeadsOfAsync(departments.Values.Distinct().ToList(), ct);
        foreach (var project in projects)
        {
            var granted = new Dictionary<Guid, ProjectRole>();
            if (project.PortfolioId is { } portfolioId)
            {
                foreach (var lead in leads.Where(l => l.PortfolioId == portfolioId))
                {
                    if (leadAccess.TryGetValue(lead.UserId, out var person) && person.IsActive)
                    {
                        granted[lead.UserId] = ProjectRoleNames.For(person.Rights);
                    }
                }

                if (departments.TryGetValue(portfolioId, out var departmentId))
                {
                    foreach (var head in heads.Where(h => h.DepartmentId == departmentId))
                    {
                        granted[head.UserId] = ProjectRoleNames.For(head.Rights);
                    }
                }
            }

            project.SyncPortfolioAccess(granted, now);
        }
    }
}
