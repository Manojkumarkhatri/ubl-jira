using Microsoft.AspNetCore.Identity;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity policy (FR-005, research R6).</summary>
public static class IdentitySetup
{
    public const int MinimumPasswordLength = 12;
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void Configure(IdentityOptions options)
    {
        // At least 12 characters, no character-class rules; UsernamePasswordValidator adds the
        // "must not contain the user name" rule and CommonPasswordValidator refuses easy-to-guess passwords.
        options.Password.RequiredLength = MinimumPasswordLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = User.AllowedUserNameCharacters;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    }
}
