using Microsoft.EntityFrameworkCore;
using Upms.Domain.Identity;

namespace Upms.Application.Common;

internal sealed class CallerContext(ICurrentUser currentUser, IAppDbContext db) : ICallerContext
{
    public async Task<CallerStatus?> GetAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new CallerStatus(u.Id, u.IsActive, u.OrganizationRole == OrganizationRole.Administrator))
            .SingleOrDefaultAsync(ct);
    }
}
