using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>First-run setup (FR-002, research R8).</summary>
internal sealed class SetupService(
    IAppDbContext db,
    UserManager<User> userManager,
    IAuditLog auditLog,
    IOptions<SetupOptions> options,
    TimeProvider time) : ISetupService
{
    public async Task<bool> IsSetupOpenAsync(CancellationToken ct)
    {
        var completed = await db.OrganizationSettings.AsNoTracking()
            .AnyAsync(s => s.Id == OrganizationSettings.SingletonId && s.SetupCompletedAt != null, ct);
        return !completed && !await db.Users.AnyAsync(
            u => u.IsActive && u.OrganizationRole == OrganizationRole.Administrator, ct);
    }

    public async Task<Result<Guid>> CreateFirstAdministratorAsync(string setupToken, string userName,
        string displayName, string email, string password, CancellationToken ct)
    {
        if (!await IsSetupOpenAsync(ct))
        {
            return AppError.Rule(ErrorCodes.SetupClosed, "Setup has already been completed.");
        }

        if (!TokenMatches(setupToken))
        {
            return AppError.Rule(ErrorCodes.InvalidSetupToken, "The setup token is not correct.");
        }

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Close setup first; a concurrent attempt finds nothing to update and stops here.
        var closed = await db.OrganizationSettings
            .Where(s => s.Id == OrganizationSettings.SingletonId && s.SetupCompletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SetupCompletedAt, now), ct);
        if (closed == 0)
        {
            return AppError.Rule(ErrorCodes.SetupClosed, "Setup has already been completed.");
        }

        var user = new User
        {
            UserName = userName.Trim(),
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            OrganizationRole = OrganizationRole.Administrator,
            CreatedAt = now,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        await auditLog.WriteAsync(AuditEventType.SetupCompleted, user.Id, user.Id, user.UserName, null, ct);
        await transaction.CommitAsync(ct);
        return user.Id;
    }

    private bool TokenMatches(string candidate)
    {
        var expected = options.Value.Token;
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(expected));
    }
}
