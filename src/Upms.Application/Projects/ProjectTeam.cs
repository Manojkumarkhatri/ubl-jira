using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

internal sealed class ProjectTeam(
    IAppDbContext db,
    IUserDirectory users,
    IUserStatusReader statuses,
    ICurrentUser currentUser,
    IMembershipAuditLog audit,
    TimeProvider time) : IProjectTeam
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
                people[m.UserId].IsActive && m.Role != ProjectRole.Viewer, people[m.UserId].IsActive, people[m.UserId].UserName))
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.UserId)
            .ToList();
    }

    public async Task<IReadOnlyList<TeamMemberInfo>> FindMembersAsync(long projectId, string term, int take, CancellationToken ct)
    {
        // A team has at most a few hundred people, so the match runs over the team read once.
        var trimmed = (term ?? "").Trim();
        return (await GetMembersAsync(projectId, ct))
            .Where(m => m.IsActive
                && (trimmed.Length == 0
                    || m.DisplayName.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase)
                    || m.UserName.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Max(0, take))
            .ToList();
    }

    public async Task<IReadOnlySet<Guid>> WhoCanSeeAsync(long projectId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = userIds.Distinct().ToList();
        var members = (await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId && ids.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync(ct)).ToHashSet();
        var people = await users.GetAsync(ids, ct);
        return people.Values
            .Where(p => p.IsActive && (p.IsAdministrator || members.Contains(p.Id)))
            .Select(p => p.Id)
            .ToHashSet();
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

    public async Task<Result<bool>> AddViewerAsync(long projectId, Guid userId, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Id == projectId, ct);
        if (project.Members.Any(m => m.UserId == userId))
        {
            return false;
        }

        if ((await users.GetAsync([userId], ct)).GetValueOrDefault(userId) is not { } person)
        {
            return AppError.NotFound("person");
        }

        if (!person.IsActive)
        {
            return AppError.Rule(ErrorCodes.AccountDeactivated,
                $"{person.DisplayName}'s account is deactivated. An administrator must reactivate it first.");
        }

        var added = project.AddMember(userId, ProjectRole.Viewer,
            currentUser.UserId ?? throw new InvalidOperationException("No signed-in user."), time.GetUtcNow());
        if (!added.IsSuccess)
        {
            return added.Error!.ToAppError();
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(MembershipChange.Added, userId, project.Key, new { Role = nameof(ProjectRole.Viewer), As = "Gate approver" }, ct);
        return true;
    }
}
