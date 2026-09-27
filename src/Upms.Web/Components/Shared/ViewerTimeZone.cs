using Upms.Application.Common;
using Upms.Application.Identity;

namespace Upms.Web.Components.Shared;

/// <summary>The time zone used to show times to the current viewer (FR-043), loaded once per circuit.</summary>
public sealed class ViewerTimeZone(IAccountService accounts)
{
    private TimeZoneInfo? _zone;

    public async Task<TimeZoneInfo> GetAsync()
    {
        if (_zone is null)
        {
            var profile = await accounts.GetProfileAsync(CancellationToken.None);
            _zone = TimeZoneResolver.Resolve(profile.Value?.EffectiveTimeZoneId, null);
        }

        return _zone;
    }

    /// <summary>Forgets the cached zone after the viewer changes it.</summary>
    public void Invalidate() => _zone = null;
}
