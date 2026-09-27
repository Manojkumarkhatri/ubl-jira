using Microsoft.EntityFrameworkCore;
using Upms.Domain.Identity;

namespace Upms.Application.Common;

/// <summary>Reads whether a user is active and an Administrator, fresh from the database.</summary>
public interface IUserStatusReader
{
    Task<CallerStatus?> GetAsync(Guid userId, CancellationToken ct);
}

internal sealed class UserStatusReader(IAppDbContext db) : IUserStatusReader
{
    public Task<CallerStatus?> GetAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new CallerStatus(u.Id, u.IsActive, u.OrganizationRole == OrganizationRole.Administrator))
            .SingleOrDefaultAsync(ct);
}
