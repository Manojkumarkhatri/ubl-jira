using Upms.Application.Identity;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeOrganizationSettingsReader(int idleTimeoutMinutes = 30, string defaultTimeZoneId = "UTC")
    : IOrganizationSettingsReader
{
    public Task<OrganizationSettingsView> GetAsync(CancellationToken ct) =>
        Task.FromResult(new OrganizationSettingsView(defaultTimeZoneId, idleTimeoutMinutes));
}
