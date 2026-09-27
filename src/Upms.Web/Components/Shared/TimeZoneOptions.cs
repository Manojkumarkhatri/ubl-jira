namespace Upms.Web.Components.Shared;

/// <summary>IANA time zones for pickers, ordered by UTC offset (FR-007, FR-043).</summary>
public static class TimeZoneOptions
{
    private static readonly Lazy<IReadOnlyList<(string Id, string Label)>> AllZones = new(Load);

    public static IReadOnlyList<(string Id, string Label)> All => AllZones.Value;

    private static List<(string Id, string Label)> Load() =>
        TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => (Zone: zone, Id: ToIanaId(zone)))
            .Where(z => z.Id is not null)
            .GroupBy(z => z.Id!)
            .Select(g => g.First())
            .OrderBy(z => z.Zone.BaseUtcOffset).ThenBy(z => z.Id, StringComparer.Ordinal)
            .Select(z => (z.Id!, $"(UTC{Offset(z.Zone.BaseUtcOffset)}) {z.Id}"))
            .ToList();

    private static string? ToIanaId(TimeZoneInfo zone) =>
        zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null;

    private static string Offset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
}
