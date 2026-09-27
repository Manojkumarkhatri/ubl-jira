using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>A project's team (Phase 2 FR-008–FR-013). Rules live in <see cref="Project"/>; this service checks the
/// caller's right and the team version, supplies which members are active, and saves each change together with its
/// audit event (FR-012).</summary>
internal sealed class ProjectMemberService(
    IAppDbContext db,
    IProjectAccess access,
    IUserDirectory users,
    IMembershipAuditLog auditLog,
    TimeProvider time) : IProjectMemberService
{
    public const int MaxPeople = 20;

    public async Task<Result<TeamView>> GetTeamAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        return allowed.IsSuccess ? await ViewAsync(allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<IReadOnlyList<PersonOption>>> FindPeopleAsync(string projectKey, string term, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var memberIds = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == allowed.Value!.ProjectId)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        var found = await users.SearchActiveAsync(term, MaxPeople, ct);
        return found.Where(u => !memberIds.Contains(u.Id))
            .Select(u => new PersonOption(u.Id, u.DisplayName, u.UserName))
            .ToList();
    }

    public Task<Result<TeamView>> AddAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion,
        CancellationToken ct) =>
        ChangeAsync(projectKey, expectedMembersVersion, async (project, allowed) =>
        {
            if (!Enum.IsDefined(role))
            {
                return Refused(AppError.Validation("Role", "Choose Project Admin, Member or Viewer."));
            }

            if ((await users.GetAsync([userId], ct)).GetValueOrDefault(userId) is not { } person)
            {
                return Refused(AppError.NotFound("user"));
            }

            if (!person.IsActive)
            {
                return Refused(AppError.Rule(ErrorCodes.AccountDeactivated,
                    $"{person.DisplayName}'s account is deactivated. An administrator must reactivate it first."));
            }

            var added = project.AddMember(userId, role, allowed.UserId, time.GetUtcNow());
            return added.IsSuccess
                ? Changed(MembershipChange.Added, userId, new { Role = role.ToString() })
                : Refused(added.Error!.ToAppError());
        }, ct);

    public Task<Result<TeamView>> ChangeRoleAsync(string projectKey, Guid userId, ProjectRole role, int expectedMembersVersion,
        CancellationToken ct) =>
        ChangeAsync(projectKey, expectedMembersVersion, async (project, _) =>
        {
            if (!Enum.IsDefined(role))
            {
                return Refused(AppError.Validation("Role", "Choose Project Admin, Member or Viewer."));
            }

            if (project.Members.FirstOrDefault(m => m.UserId == userId) is not { } member)
            {
                return Refused(AppError.NotFound("member"));
            }

            var previous = member.Role;
            if (project.ChangeMemberRole(member, role, await ActiveMembersAsync(project, ct), time.GetUtcNow()) is { } error)
            {
                return Refused(error.ToAppError());
            }

            return previous == role
                ? Unchanged()
                : Changed(MembershipChange.RoleChanged, userId, new { From = previous.ToString(), To = role.ToString() });
        }, ct);

    public Task<Result<TeamView>> RemoveAsync(string projectKey, Guid userId, int expectedMembersVersion, CancellationToken ct) =>
        ChangeAsync(projectKey, expectedMembersVersion, async (project, _) =>
        {
            if (project.Members.FirstOrDefault(m => m.UserId == userId) is not { } member)
            {
                return Refused(AppError.NotFound("member"));
            }

            var role = member.Role;
            return project.RemoveMember(member, await ActiveMembersAsync(project, ct), time.GetUtcNow()) is { } error
                ? Refused(error.ToAppError())
                : Changed(MembershipChange.Removed, userId, new { Role = role.ToString() });
        }, ct);

    /// <summary>Checks the right and the version, applies the change, then saves it and its audit event in one
    /// transaction.</summary>
    private async Task<Result<TeamView>> ChangeAsync(string projectKey, int expectedMembersVersion,
        Func<Project, ProjectAccessInfo, Task<Outcome>> change, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var info = allowed.Value!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Id == info.ProjectId, ct);
        if (project.MembersVersion != expectedMembersVersion)
        {
            return await ConflictAsync(info, ct);
        }

        var outcome = await change(project, info);
        if (outcome.Error is { } error)
        {
            return error;
        }

        if (outcome.Audit is not { } audit)
        {
            return await ViewAsync(info, ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another team change (the version check) or the same person added at the same moment (unique member).
            await transaction.RollbackAsync(ct);
            return await ConflictAsync(info, ct);
        }

        await auditLog.WriteAsync(audit.Change, audit.UserId, project.Key, audit.Details, ct);
        await transaction.CommitAsync(ct);
        return await ViewAsync(info, ct);
    }

    private async Task<IReadOnlySet<Guid>> ActiveMembersAsync(Project project, CancellationToken ct) =>
        (await users.GetAsync(project.Members.Select(m => m.UserId).ToList(), ct)).Values
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToHashSet();

    private async Task<AppError> ConflictAsync(ProjectAccessInfo allowed, CancellationToken ct) =>
        AppError.Conflict("The team was changed by someone else. The latest team is shown; check it and try again.",
            await ViewAsync(allowed, ct));

    private async Task<TeamView> ViewAsync(ProjectAccessInfo allowed, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == allowed.ProjectId)
            .Select(p => new { p.Key, p.Name, p.MembersVersion })
            .SingleAsync(ct);
        var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == allowed.ProjectId).ToListAsync(ct);
        var people = await users.GetAsync(members.ConvertAll(m => m.UserId), ct);
        var views = members
            .Select(m => people.GetValueOrDefault(m.UserId) is { } person
                ? new MemberView(m.UserId, person.DisplayName, person.UserName, m.Role, person.IsActive, m.UserId == allowed.UserId, m.AddedAt)
                : new MemberView(m.UserId, "Unknown user", "", m.Role, false, m.UserId == allowed.UserId, m.AddedAt))
            .OrderBy(v => v.Role == ProjectRole.ProjectAdmin ? 0 : 1)
            .ThenBy(v => v.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(v => v.UserName, StringComparer.Ordinal)
            .ToList();
        return new TeamView(project.Key, project.Name, project.MembersVersion, allowed.CanManage, views, allowed.IsAdministrator);
    }

    private static Outcome Refused(AppError error) => new(error, null);

    private static Outcome Unchanged() => new(null, null);

    private static Outcome Changed(MembershipChange change, Guid userId, object details) => new(null, new AuditEntry(change, userId, details));

    private sealed record Outcome(AppError? Error, AuditEntry? Audit);

    private sealed record AuditEntry(MembershipChange Change, Guid UserId, object Details);
}
