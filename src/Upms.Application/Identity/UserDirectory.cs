using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;

namespace Upms.Application.Identity;

internal sealed class UserDirectory(IAppDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserDisplay>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserDisplay(u.Id, u.DisplayName, u.IsActive))
            .ToDictionaryAsync(u => u.Id, ct);
    }
}
