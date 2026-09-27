namespace Upms.Application.Identity;

/// <summary>Read-only access to organization settings (time zone default, idle timeout).</summary>
public interface IOrganizationSettingsReader
{
    Task<OrganizationSettingsView> GetAsync(CancellationToken ct);
}

public sealed record OrganizationSettingsView(string DefaultTimeZoneId, int IdleTimeoutMinutes);
