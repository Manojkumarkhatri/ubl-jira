using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>The signed-in user's own account (FR-007, FR-043).</summary>
internal sealed class AccountService(
    IAppDbContext db,
    UserManager<User> userManager,
    ICallerContext caller,
    IAuditLog auditLog) : IAccountService
{
    public async Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct)
    {
        if (await LoadCallerAsync(ct) is not { } user)
        {
            return AppError.Forbidden();
        }

        var defaultTimeZone = await db.OrganizationSettings.AsNoTracking()
            .Select(s => s.DefaultTimeZoneId).SingleAsync(ct);
        return new MyProfile(user.Id, user.UserName!, user.DisplayName, user.Email!, user.TimeZoneId,
            user.TimeZoneId ?? defaultTimeZone, user.IsAdministrator);
    }

    public async Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct)
    {
        var name = displayName?.Trim() ?? "";
        if (name.Length == 0)
        {
            return AppError.Validation("DisplayName", "Enter a display name.");
        }

        if (name.Length > User.DisplayNameMaxLength)
        {
            return AppError.Validation("DisplayName", $"The display name can have at most {User.DisplayNameMaxLength} characters.");
        }

        var zone = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim();
        if (zone is not null && !TimeZoneResolver.IsValidIanaId(zone))
        {
            return AppError.Validation("TimeZoneId", "Choose a valid time zone.");
        }

        if (await LoadCallerAsync(ct) is not { } user)
        {
            return AppError.Forbidden();
        }

        user.DisplayName = name;
        user.TimeZoneId = zone;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Result.Ok() : IdentityErrors.ToAppError(result);
    }

    public async Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct)
    {
        if (await LoadCallerAsync(ct) is not { } user)
        {
            return AppError.Forbidden();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result, passwordField: "NewPassword");
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await userManager.UpdateAsync(user);
        }

        await auditLog.WriteAsync(AuditEventType.PasswordChanged, user.Id, user.UserName ?? "", null, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    private async Task<User?> LoadCallerAsync(CancellationToken ct) =>
        await caller.GetAsync(ct) is { IsActive: true } status
            ? await userManager.FindByIdAsync(status.UserId.ToString())
            : null;
}
