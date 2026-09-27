namespace Upms.Application.Common;

/// <summary>Resolves the time zone used to show times to a user (FR-043): their own choice, else the
/// organization default, else UTC.</summary>
public sealed class TimeZoneResolver
{
    /// <summary>True when <paramref name="timeZoneId"/> is a known IANA time zone ID.</summary>
    public static bool IsValidIanaId(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
        && zone.HasIanaId;

    public static TimeZoneInfo Resolve(string? userTimeZoneId, string? organizationTimeZoneId)
    {
        foreach (var id in new[] { userTimeZoneId, organizationTimeZoneId })
        {
            if (IsValidIanaId(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id!, out var zone))
            {
                return zone;
            }
        }

        return TimeZoneInfo.Utc;
    }
}
