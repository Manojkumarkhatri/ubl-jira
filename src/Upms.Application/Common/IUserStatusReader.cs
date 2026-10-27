using Microsoft.EntityFrameworkCore;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Common;

/// <summary>Reads whether a user is active and an Administrator, with their type and ticks, fresh from the database.</summary>
public interface IUserStatusReader
{
    Task<CallerStatus?> GetAsync(Guid userId, CancellationToken ct);
}

internal sealed class UserStatusReader(IAppDbContext db) : IUserStatusReader
{
    public async Task<CallerStatus?> GetAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.IsActive,
                u.OrganizationRole,
                u.UserType,
                u.DepartmentId,
                u.RightsGranted,
                u.RightsDenied,
                Defaults = db.UserTypeRights.Where(r => r.Type == u.UserType).Select(r => (Rights?)r.Rights).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var administrator = row.OrganizationRole == OrganizationRole.Administrator;
        var type = administrator ? UserType.Admin : row.UserType == UserType.Admin ? UserType.Member : row.UserType;
        return new CallerStatus(row.Id, row.IsActive, administrator, type, row.DepartmentId,
            RightsCatalog.Effective(type, row.Defaults ?? RightsCatalog.Defaults(type), row.RightsGranted, row.RightsDenied));
    }
}
