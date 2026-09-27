using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

internal sealed class ProjectTeam(IAppDbContext db, IUserDirectory users, IUserStatusReader statuses) : IProjectTeam
{
    public async Task<IReadOnlyList<TeamMemberInfo>> GetMembersAsync(long projectId, CancellationToken ct)
    {
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Select(m => new { m.UserId, m.Role })
            .ToListAsync(ct);
        var people = await users.GetAsync(members.ConvertAll(m => m.UserId), ct);
        return members
            .Where(m => people.ContainsKey(m.UserId))
            .Select(m => new TeamMemberInfo(m.UserId, people[m.UserId].DisplayName,
                people[m.UserId].IsActive && m.Role != ProjectRole.Viewer))
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.UserId)
            .ToList();
    }

    public async Task<bool> CanBeAssignedAsync(long projectId, Guid userId, CancellationToken ct)
    {
        var role = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .FirstOrDefaultAsync(ct);
        return role is ProjectRole.ProjectAdmin or ProjectRole.Member && await statuses.GetAsync(userId, ct) is { IsActive: true };
    }

    public async Task<IReadOnlyList<ProjectRef>> VisibleAmongAsync(Guid userId, IReadOnlyCollection<long> projectIds,
        CancellationToken ct)
    {
        if (projectIds.Count == 0 || await statuses.GetAsync(userId, ct) is not { IsActive: true } person)
        {
            return [];
        }

        var ids = projectIds.Distinct().ToList();
        var projects = db.Projects.AsNoTracking().Where(p => ids.Contains(p.Id));
        if (!person.IsAdministrator)
        {
            projects = projects.Where(p => db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId));
        }

        return await projects.Select(p => new ProjectRef(p.Id, p.Key, p.Name)).ToListAsync(ct);
    }
}
