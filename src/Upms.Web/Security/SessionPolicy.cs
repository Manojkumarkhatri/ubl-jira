using System.Globalization;
using System.Security.Claims;
using Upms.Application.Identity;

namespace Upms.Web.Security;

/// <summary>When a sign-in session is over, checked on every request that carries the cookie (OWASP ASVS 3.3.1,
/// 3.3.2): after signing out or the idle timeout (the session ID is marked expired), and 12 hours after signing
/// in even while in use.</summary>
public static class SessionPolicy
{
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    public static bool HasEnded(ClaimsPrincipal principal, SessionActivityRegistry registry, DateTimeOffset now)
    {
        if (principal.FindFirst(UpmsClaimTypes.SessionId)?.Value is { } sessionId && registry.IsExpired(sessionId))
        {
            return true;
        }

        // Cookies from before this rule have no sign-in time; they end with the idle timeout.
        return long.TryParse(principal.FindFirst(UpmsClaimTypes.SignedInAt)?.Value, NumberStyles.Integer,
                   CultureInfo.InvariantCulture, out var signedIn)
            && now - DateTimeOffset.FromUnixTimeSeconds(signedIn) > AbsoluteLifetime;
    }
}
