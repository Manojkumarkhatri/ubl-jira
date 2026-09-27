using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Time.Testing;
using Upms.Application.Identity;
using Upms.Web.Security;

namespace Upms.Web.Tests.Security;

/// <summary>When a sign-in session is over (OWASP ASVS 3.3.1, 3.3.2).</summary>
public sealed class SessionPolicyTests
{
    private static readonly DateTimeOffset SignedIn = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(SignedIn);

    private static ClaimsPrincipal Principal(string sessionId, DateTimeOffset? signedIn) => new(new ClaimsIdentity(
        new[] { new Claim(UpmsClaimTypes.SessionId, sessionId) }
            .Concat(signedIn is { } at
                ? [new Claim(UpmsClaimTypes.SignedInAt, at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))]
                : []),
        "test"));

    [Fact]
    public void A_session_is_valid_until_it_ends()
    {
        var registry = new SessionActivityRegistry(_time);

        Assert.False(SessionPolicy.HasEnded(Principal("s1", SignedIn), registry, SignedIn.AddHours(11).AddMinutes(59)));
    }

    [Fact]
    public void Signing_out_or_the_idle_timeout_ends_the_session_for_every_copy_of_the_cookie()
    {
        var registry = new SessionActivityRegistry(_time);
        registry.Expire("s1");

        Assert.True(SessionPolicy.HasEnded(Principal("s1", SignedIn), registry, SignedIn.AddMinutes(5)));
        Assert.False(SessionPolicy.HasEnded(Principal("s2", SignedIn), registry, SignedIn.AddMinutes(5)));
    }

    [Fact]
    public void A_session_ends_twelve_hours_after_signing_in_even_while_in_use()
    {
        var registry = new SessionActivityRegistry(_time);

        Assert.True(SessionPolicy.HasEnded(Principal("s1", SignedIn), registry, SignedIn.AddHours(12).AddMinutes(1)));
    }

    [Fact]
    public void Older_cookies_without_a_sign_in_time_end_with_the_idle_timeout_only()
    {
        var registry = new SessionActivityRegistry(_time);

        Assert.False(SessionPolicy.HasEnded(Principal("s1", null), registry, SignedIn.AddDays(2)));
    }
}
