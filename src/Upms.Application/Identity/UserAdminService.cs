using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>Minimal account management for administrators (FR-003, FR-004, FR-010).</summary>
internal sealed class UserAdminService(
    IAppDbContext db,
    UserManager<User> userManager,
    ICallerContext caller,
    IAuditLog auditLog,
    ITemporaryPasswordGenerator passwords,
    TimeProvider time) : IUserAdminService
{
    public async Task<Result<Page<UserSummary>>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct)
    {
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        page = page.Normalized();
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.UserName!.Contains(term) || u.DisplayName.Contains(term) || u.Email!.Contains(term));
        }

        var now = time.GetUtcNow();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.UserName)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(u => new UserSummary(u.Id, u.UserName!, u.DisplayName, u.Email!, u.OrganizationRole, u.IsActive,
                u.LockoutEnd != null && u.LockoutEnd > now, u.CreatedAt, u.LastSignInAt))
            .ToListAsync(ct);
        return new Page<UserSummary>(items, total, page.Page, page.PageSize);
    }

    public async Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email, CancellationToken ct)
    {
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        var user = new User
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            MustChangePassword = true,
            CreatedAt = time.GetUtcNow(),
        };
        var temporaryPassword = passwords.Generate(user.UserName);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        await auditLog.WriteAsync(AuditEventType.UserCreated, user.Id, user.UserName,
            new { user.UserName, user.DisplayName, user.Email }, ct);
        await transaction.CommitAsync(ct);
        return new CreatedUser(user.Id, user.UserName, temporaryPassword);
    }

    public async Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct)
    {
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        var temporaryPassword = passwords.Generate(user.UserName ?? "");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        user.MustChangePassword = true;
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.UpdateAsync(user);
        await auditLog.WriteAsync(AuditEventType.PasswordReset, user.Id, user.UserName ?? "", null, ct);
        await transaction.CommitAsync(ct);
        return temporaryPassword;
    }

    public async Task<Result> DeactivateAsync(Guid userId, CancellationToken ct)
    {
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        // Serializable so that two administrators cannot deactivate each other at the same moment.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        if (!user.IsActive)
        {
            return Result.Ok();
        }

        if (user.IsAdministrator && !await db.Users.AnyAsync(u =>
                u.Id != user.Id && u.IsActive && u.OrganizationRole == OrganizationRole.Administrator, ct))
        {
            return AppError.Rule(ErrorCodes.LastAdministrator,
                "This is the last active administrator. Add another administrator before deactivating this account.");
        }

        user.Deactivate(time.GetUtcNow());
        // Rotating the security stamp ends the user's open sessions within a minute (research R6).
        await userManager.UpdateSecurityStampAsync(user);
        await auditLog.WriteAsync(AuditEventType.UserDeactivated, user.Id, user.UserName ?? "", null, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ReactivateAsync(Guid userId, CancellationToken ct)
    {
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        if (user.IsActive)
        {
            return Result.Ok();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        user.Reactivate();
        await userManager.UpdateAsync(user);
        await auditLog.WriteAsync(AuditEventType.UserReactivated, user.Id, user.UserName ?? "", null, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    private async Task<AppError?> RequireAdministratorAsync(CancellationToken ct) =>
        await caller.GetAsync(ct) is { IsActive: true, IsAdministrator: true }
            ? null
            : AppError.Forbidden("Only administrators can manage accounts.");
}
