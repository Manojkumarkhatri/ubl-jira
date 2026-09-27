using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

internal sealed class OrganizationSettingsReader(IAppDbContext db) : IOrganizationSettingsReader
{
    public Task<OrganizationSettingsView> GetAsync(CancellationToken ct) =>
        db.OrganizationSettings.AsNoTracking()
            .Where(s => s.Id == OrganizationSettings.SingletonId)
            .Select(s => new OrganizationSettingsView(s.DefaultTimeZoneId, s.IdleTimeoutMinutes))
            .SingleAsync(ct);
}
