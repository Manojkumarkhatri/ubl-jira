using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Upms.Application.Identity;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>Adds the display name, the forced-password-change flag and a per-sign-in session ID to the
/// cookie. Roles are not stored in the cookie: rights are read from the database on every call (R7).</summary>
public sealed class UpmsClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(UpmsClaimTypes.DisplayName, user.DisplayName));
        identity.AddClaim(new Claim(UpmsClaimTypes.SessionId, Guid.NewGuid().ToString("N")));
        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(UpmsClaimTypes.MustChangePassword, "true"));
        }

        return identity;
    }
}
