using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Infrastructure.Identity;

/// <summary>Sign-in with the Phase 1 rules: deactivated users are refused, the last sign-in time is kept,
/// and every success, failure and lockout is audited without the password (FR-001, FR-004, FR-010).</summary>
public sealed class AuditingSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<User>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation,
    IAuditLog auditLog,
    TimeProvider time)
    : SignInManager<User>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(User user) =>
        user.IsActive && await base.CanSignInAsync(user);

    public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool isPersistent,
        bool lockoutOnFailure)
    {
        var user = await UserManager.FindByNameAsync(userName);
        if (user is null)
        {
            await auditLog.WriteAsync(AuditEventType.SignInFailed, null, null, userName,
                new { reason = "unknown user name" }, CancellationToken.None);
            return SignInResult.Failed;
        }

        var wasLockedOut = await UserManager.IsLockedOutAsync(user);
        var result = await base.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);

        if (result.Succeeded)
        {
            user.LastSignInAt = time.GetUtcNow();
            await UserManager.UpdateAsync(user);
            await auditLog.WriteAsync(AuditEventType.SignInSucceeded, user.Id, user.Id, userName, null, CancellationToken.None);
        }
        else if (result.IsLockedOut && !wasLockedOut)
        {
            await auditLog.WriteAsync(AuditEventType.SignInFailed, user.Id, user.Id, userName,
                new { reason = "wrong password" }, CancellationToken.None);
            await auditLog.WriteAsync(AuditEventType.LockedOut, user.Id, user.Id, userName,
                new { minutes = Options.Lockout.DefaultLockoutTimeSpan.TotalMinutes }, CancellationToken.None);
        }
        else
        {
            var reason = result.IsLockedOut ? "locked out" : result.IsNotAllowed ? "account deactivated" : "wrong password";
            await auditLog.WriteAsync(AuditEventType.SignInFailed, user.Id, user.Id, userName, new { reason },
                CancellationToken.None);
        }

        return result;
    }
}
