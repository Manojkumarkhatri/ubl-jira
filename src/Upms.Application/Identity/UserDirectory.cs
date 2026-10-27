using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

internal sealed class UserDirectory(IAppDbContext db) : IUserDirectory
{
    public const int MaxSearchResults = 20;

    public async Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserDisplay>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserDisplay(u.Id, u.DisplayName, u.IsActive, u.UserName!,
                u.OrganizationRole == OrganizationRole.Administrator,
                u.OrganizationRole == OrganizationRole.Administrator ? UserType.Admin : u.UserType))
            .ToDictionaryAsync(u => u.Id, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, RecipientInfo>> GetRecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, RecipientInfo>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new RecipientInfo(u.Id, u.DisplayName, u.Email!, u.IsActive, u.EmailNotifications))
            .ToDictionaryAsync(u => u.Id, ct);
    }

    public async Task<IReadOnlyList<UserDisplay>> SearchActiveAsync(string term, int take, CancellationToken ct)
    {
        var trimmed = (term ?? "").Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive
                && (u.DisplayName.Contains(trimmed) || u.UserName!.Contains(trimmed) || u.Email!.Contains(trimmed)))
            .OrderBy(u => u.DisplayName).ThenBy(u => u.UserName)
            .Take(Math.Clamp(take, 1, MaxSearchResults))
            .Select(u => new UserDisplay(u.Id, u.DisplayName, u.IsActive, u.UserName!,
                u.OrganizationRole == OrganizationRole.Administrator,
                u.OrganizationRole == OrganizationRole.Administrator ? UserType.Admin : u.UserType))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, PersonAccess>> AccessOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, PersonAccess>();
        }

        var ids = userIds.Distinct().ToList();
        return (await ReadAsync(db.Users.Where(u => ids.Contains(u.Id)), ct)).ToDictionary(p => p.UserId);
    }

    public async Task<IReadOnlyList<PersonAccess>> HeadsOfAsync(IReadOnlyCollection<long> departmentIds, CancellationToken ct)
    {
        if (departmentIds.Count == 0)
        {
            return [];
        }

        var ids = departmentIds.Distinct().ToList();
        return await ReadAsync(db.Users.Where(u => u.IsActive && u.UserType == UserType.Head
            && u.OrganizationRole != OrganizationRole.Administrator && u.DepartmentId != null && ids.Contains(u.DepartmentId.Value)), ct);
    }

    public async Task<IReadOnlyList<Guid>> PeopleOfTypeAsync(UserType type, CancellationToken ct) =>
        type == UserType.Admin
            ? await db.Users.AsNoTracking().Where(u => u.OrganizationRole == OrganizationRole.Administrator).Select(u => u.Id).ToListAsync(ct)
            : await db.Users.AsNoTracking().Where(u => u.UserType == type && u.OrganizationRole != OrganizationRole.Administrator)
                .Select(u => u.Id).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<long, string>> DepartmentNamesAsync(CancellationToken ct) =>
        await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);

    /// <summary>The people with their effective ticks, from their type's row and their own.</summary>
    private async Task<List<PersonAccess>> ReadAsync(IQueryable<User> users, CancellationToken ct)
    {
        var defaults = await db.UserTypeRights.AsNoTracking().ToDictionaryAsync(r => r.Type, r => r.Rights, ct);
        var rows = await users.AsNoTracking()
            .Select(u => new { u.Id, u.UserType, u.OrganizationRole, u.DepartmentId, u.RightsGranted, u.RightsDenied, u.IsActive })
            .ToListAsync(ct);
        return rows.Select(u =>
        {
            var type = u.OrganizationRole == OrganizationRole.Administrator ? UserType.Admin
                : u.UserType == UserType.Admin ? UserType.Member : u.UserType;
            var rights = RightsCatalog.Effective(type, defaults.TryGetValue(type, out var d) ? d : RightsCatalog.Defaults(type),
                u.RightsGranted, u.RightsDenied);
            return new PersonAccess(u.Id, type, u.DepartmentId, rights, u.IsActive);
        }).ToList();
    }
}
