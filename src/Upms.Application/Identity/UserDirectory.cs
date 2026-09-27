using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;

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
            .Select(u => new UserDisplay(u.Id, u.DisplayName, u.IsActive, u.UserName!))
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
            .Select(u => new UserDisplay(u.Id, u.DisplayName, u.IsActive, u.UserName!))
            .ToListAsync(ct);
    }
}
