namespace Upms.Web.Components.Shared;

/// <summary>Today's calendar date for the viewer, in their time zone (Phase 2 FR-042, research R9). Start and due dates
/// are the same for everyone; whether one has passed depends on where the viewer is.</summary>
public sealed class ViewerToday(ViewerTimeZone zone, TimeProvider time)
{
    public async Task<DateOnly> GetAsync() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), await zone.GetAsync()).DateTime);
}
