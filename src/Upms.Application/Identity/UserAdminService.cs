using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

/// <summary>Account management (FR-003, FR-004, FR-008, FR-010), with the five user types: an Admin manages everyone; a Head
/// with the ticks manages the people of their own department, below Head.</summary>
internal sealed class UserAdminService(
    IAppDbContext db,
    UserManager<User> userManager,
    ICallerContext caller,
    IAuditLog auditLog,
    ITemporaryPasswordGenerator passwords,
    IAdministratorLock administrators,
    ISpaceAccessSync spaceAccess,
    TimeProvider time) : IUserAdminService
{
    public Task<Result<Page<UserSummary>>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct) =>
        FindUsersAsync(new UserQuery(search), page, ct);

    public async Task<Result<Page<UserSummary>>> FindUsersAsync(UserQuery filter, PageRequest page, CancellationToken ct)
    {
        var rights = await RightsAsync(ct);
        if (!rights.CanList)
        {
            return AppError.Forbidden("You can't manage users. Ask an Admin.");
        }

        page = page.Normalized();
        var query = Scope(rights);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(u => u.UserName!.Contains(term) || u.DisplayName.Contains(term) || u.Email!.Contains(term));
        }

        if (filter.Type is { } type)
        {
            // As TypeOf: the administrator role makes an Admin, and an Admin type without it is a Member.
            query = type switch
            {
                UserType.Admin => query.Where(u => u.OrganizationRole == OrganizationRole.Administrator),
                UserType.Member => query.Where(u => u.OrganizationRole != OrganizationRole.Administrator
                    && (u.UserType == UserType.Member || u.UserType == UserType.Admin)),
                _ => query.Where(u => u.OrganizationRole != OrganizationRole.Administrator && u.UserType == type),
            };
        }

        if (filter.Active is { } active)
        {
            query = query.Where(u => u.IsActive == active);
        }

        var now = time.GetUtcNow();
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(u => u.DisplayName).ThenBy(u => u.UserName)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.DisplayName,
                u.Email,
                u.OrganizationRole,
                u.IsActive,
                Locked = u.LockoutEnd != null && u.LockoutEnd > now,
                u.CreatedAt,
                u.LastSignInAt,
                u.UserType,
                u.DepartmentId,
                Department = db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                u.RightsGranted,
                u.RightsDenied,
            })
            .ToListAsync(ct);
        var defaults = await TypeDefaultsAsync(ct);
        var items = rows.ConvertAll(u =>
        {
            var type = TypeOf(u.OrganizationRole, u.UserType);
            return new UserSummary(u.Id, u.UserName!, u.DisplayName, u.Email!, u.OrganizationRole, u.IsActive, u.Locked, u.CreatedAt,
                u.LastSignInAt)
            {
                Type = type,
                DepartmentId = u.DepartmentId,
                Department = u.Department,
                Granted = u.RightsGranted,
                Denied = u.RightsDenied,
                Rights = RightsCatalog.Effective(type, defaults[type], u.RightsGranted, u.RightsDenied),
            };
        });
        return new Page<UserSummary>(items, total, page.Page, page.PageSize);
    }

    public async Task<IReadOnlyDictionary<UserType, int>> TypeCountsAsync(CancellationToken ct)
    {
        var rights = await RightsAsync(ct);
        if (!rights.CanList)
        {
            return new Dictionary<UserType, int>();
        }

        var groups = await Scope(rights)
            .GroupBy(u => new { u.OrganizationRole, u.UserType })
            .Select(g => new { g.Key.OrganizationRole, g.Key.UserType, Count = g.Count() })
            .ToListAsync(ct);
        return groups.GroupBy(g => TypeOf(g.OrganizationRole, g.UserType)).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
    }

    /// <summary>Everyone for an Admin; otherwise the caller's own department.</summary>
    private IQueryable<User> Scope(PeopleRights rights) =>
        rights.IsAdministrator ? db.Users.AsNoTracking() : db.Users.AsNoTracking().Where(u => u.DepartmentId == rights.DepartmentId);

    public Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email, CancellationToken ct) =>
        AddPersonAsync(new NewPerson(userName, displayName, email, UserType.Member, null), ct);

    public async Task<Result<CreatedUser>> AddPersonAsync(NewPerson person, CancellationToken ct)
    {
        var rights = await RightsAsync(ct);
        if (!rights.CanAdd)
        {
            return AppError.Forbidden("Only administrators can manage accounts.");
        }

        if (!rights.Types.Contains(person.Type))
        {
            return AppError.Validation("Type", "Choose one of the types you can give.");
        }

        var departmentId = rights.IsAdministrator ? person.DepartmentId : rights.DepartmentId;
        if (departmentId is { } id && !await db.Departments.AnyAsync(d => d.Id == id, ct))
        {
            return AppError.Validation("DepartmentId", "Choose a department from the list.");
        }

        if (person.Type == UserType.Head && departmentId is null)
        {
            return AppError.Validation("DepartmentId", "A Head needs a department.");
        }

        var user = new User
        {
            UserName = (person.UserName ?? "").Trim(),
            Email = (person.Email ?? "").Trim(),
            DisplayName = (person.DisplayName ?? "").Trim(),
            MustChangePassword = true,
            CreatedAt = time.GetUtcNow(),
            UserType = person.Type,
            OrganizationRole = person.Type == UserType.Admin ? OrganizationRole.Administrator : OrganizationRole.User,
            DepartmentId = departmentId,
        };
        var temporaryPassword = passwords.Generate(user.UserName);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        await auditLog.WriteAsync(AuditEventType.UserCreated, user.Id, user.UserName,
            new { user.UserName, user.DisplayName, user.Email, Type = user.UserType.ToString(), user.DepartmentId }, ct);
        if (user.UserType == UserType.Head)
        {
            // A new Head sees their department's spaces at once.
            await spaceAccess.SyncPeopleAsync([user.Id], ct);
        }

        await transaction.CommitAsync(ct);
        return new CreatedUser(user.Id, user.UserName, temporaryPassword);
    }

    public async Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        if (await RequireAccountManagerAsync(user, ct) is { } denied)
        {
            return denied;
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
        await using var transaction = await BeginAdministratorChangeAsync(ct);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        if (await RequireAccountManagerAsync(user, ct) is { } denied)
        {
            return denied;
        }

        if (!user.IsActive)
        {
            return Result.Ok();
        }

        if (user.IsAdministrator && !await AnotherActiveAdministratorExistsAsync(user.Id, ct))
        {
            return AppError.Rule(ErrorCodes.LastAdministrator,
                "This is the last active administrator. Make someone else an administrator before deactivating this account.");
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
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        if (await RequireAccountManagerAsync(user, ct) is { } denied)
        {
            return denied;
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

    public async Task<Result> ChangeRoleAsync(Guid userId, OrganizationRole role, CancellationToken ct)
    {
        if (!Enum.IsDefined(role))
        {
            return AppError.Validation("role", "Choose Administrator or User.");
        }

        await using var transaction = await BeginAdministratorChangeAsync(ct);
        if (await RequireAdministratorAsync(ct) is { } denied)
        {
            return denied;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AppError.NotFound("user");
        }

        var previous = user.OrganizationRole;
        if (previous == role)
        {
            return Result.Ok();
        }

        if (await RoleRuleAsync(user, role, ct) is { } refused)
        {
            return refused;
        }

        // No security stamp change: rights are read from the database on every call, so the new role applies to the
        // person's next action without signing them out (research R7).
        user.OrganizationRole = role;
        user.UserType = role == OrganizationRole.Administrator ? UserType.Admin
            : user.UserType == UserType.Admin ? UserType.Member : user.UserType;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        await auditLog.WriteAsync(AuditEventType.RoleChanged, user.Id, user.UserName ?? "",
            new { From = previous.ToString(), To = role.ToString() }, ct);
        await spaceAccess.SyncPeopleAsync([user.Id], ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateAccessAsync(Guid userId, AccessChange change, CancellationToken ct)
    {
        if (!Enum.IsDefined(change.Type))
        {
            return AppError.Validation("Type", "Choose a type.");
        }

        await using var transaction = await BeginAdministratorChangeAsync(ct);
        var rights = await RightsAsync(ct);
        if (!rights.CanSetAccess)
        {
            return AppError.Forbidden("You don't have the \"Change users' type and rights\" right. Ask an Admin.");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || (!rights.IsAdministrator && user.DepartmentId != rights.DepartmentId))
        {
            return AppError.NotFound("user");
        }

        var currentType = TypeOf(user.OrganizationRole, user.UserType);
        if (!rights.Manages(currentType, user.DepartmentId))
        {
            return AppError.Forbidden("You can only change people below your own type in your department.");
        }

        if (change.Type != currentType && !rights.Types.Contains(change.Type))
        {
            return AppError.Forbidden($"You can't make people {change.Type.DisplayName()}.");
        }

        var departmentId = rights.IsAdministrator ? change.DepartmentId : rights.DepartmentId;
        if (departmentId is { } id && !await db.Departments.AnyAsync(d => d.Id == id, ct))
        {
            return AppError.Validation("DepartmentId", "Choose a department from the list.");
        }

        if (change.Type == UserType.Head && departmentId is null)
        {
            return AppError.Validation("DepartmentId", "A Head needs a department.");
        }

        // Outside the Admins, people give or take only the ticks they have themselves; the others stay as they were. Ticks
        // beyond the type's limit are never kept.
        var mask = rights.Rights;
        var denied = ((change.Denied & mask) | (user.RightsDenied & ~mask)) & Rights.All;
        var granted = ((change.Granted & mask) | (user.RightsGranted & ~mask)) & RightsCatalog.Allowed(change.Type) & ~denied;
        var role = change.Type == UserType.Admin ? OrganizationRole.Administrator : OrganizationRole.User;
        if (role != user.OrganizationRole && await RoleRuleAsync(user, role, ct) is { } refused)
        {
            return refused;
        }

        var before = new { Type = currentType.ToString(), user.DepartmentId, Granted = user.RightsGranted, Denied = user.RightsDenied };
        user.UserType = change.Type;
        user.OrganizationRole = role;
        user.DepartmentId = departmentId;
        user.RightsGranted = granted;
        user.RightsDenied = denied;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityErrors.ToAppError(result);
        }

        await auditLog.WriteAsync(AuditEventType.AccessChanged, user.Id, user.UserName ?? "",
            new
            {
                From = before,
                To = new { Type = change.Type.ToString(), user.DepartmentId, Granted = user.RightsGranted, Denied = user.RightsDenied },
            }, ct);
        await spaceAccess.SyncPeopleAsync([user.Id], ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<PeopleRights> RightsAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return new PeopleRights(false, false, false, false, null, [], Rights.None) { Type = UserType.ReadOnly };
        }

        if (me.IsAdministrator)
        {
            return new PeopleRights(true, true, true, true, null, UserTypes.Ordered, Rights.All) { CanManageAccounts = true };
        }

        if (me.DepartmentId is null || !me.Rights.HasAny(RightsCatalog.ManagesUsers))
        {
            return new PeopleRights(false, false, false, false, null, [], Rights.None) { Type = me.Type };
        }

        // Below their own type, and only the types they have the tick to add: a Head with "Add Portfolio lead users" adds
        // Portfolio leads, never Heads (owner, 2026-10-26).
        var types = UserTypes.Ordered.Where(t => t.Rank() < me.Type.Rank() && me.Can(RightsCatalog.AddRightFor(t))).ToList();
        var canSet = me.Can(Rights.SetAccess);
        var canAccounts = me.Can(Rights.ManageAccounts);
        return new PeopleRights(true, types.Count > 0, canSet, false, me.DepartmentId, types, me.Rights)
        {
            Type = me.Type,
            CanManageAccounts = canAccounts,
        };
    }

    /// <summary>An administrator is an Admin whatever the stored type says; an Admin type without the role is a Member.</summary>
    private static UserType TypeOf(OrganizationRole role, UserType type) =>
        role == OrganizationRole.Administrator ? UserType.Admin : type == UserType.Admin ? UserType.Member : type;

    private async Task<Dictionary<UserType, Rights>> TypeDefaultsAsync(CancellationToken ct)
    {
        var stored = await db.UserTypeRights.AsNoTracking().ToDictionaryAsync(r => r.Type, r => r.Rights, ct);
        return UserTypes.Ordered.ToDictionary(t => t, t => stored.TryGetValue(t, out var r) ? r : RightsCatalog.Defaults(t));
    }

    /// <summary>The administrator rules: only an active account becomes one, and the last active one stays.</summary>
    private async Task<AppError?> RoleRuleAsync(User user, OrganizationRole role, CancellationToken ct)
    {
        if (role == OrganizationRole.Administrator && !user.IsActive)
        {
            return AppError.Rule(ErrorCodes.AccountDeactivated,
                "This account is deactivated. Reactivate it before making it an administrator.");
        }

        if (role != OrganizationRole.Administrator && user.IsAdministrator && user.IsActive
            && !await AnotherActiveAdministratorExistsAsync(user.Id, ct))
        {
            return AppError.Rule(ErrorCodes.LastAdministrator,
                "This is the last active administrator. Make someone else an administrator first.");
        }

        return null;
    }

    /// <summary>Starts a transaction that holds the administrators lock. The caller's own rights are checked after
    /// this, inside the lock: another administrator may have just removed them.</summary>
    private async Task<IDbContextTransaction> BeginAdministratorChangeAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await administrators.AcquireAsync(ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private Task<bool> AnotherActiveAdministratorExistsAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id != userId && u.IsActive && u.OrganizationRole == OrganizationRole.Administrator, ct);

    private async Task<AppError?> RequireAdministratorAsync(CancellationToken ct) =>
        await caller.GetAsync(ct) is { IsActive: true, IsAdministrator: true }
            ? null
            : AppError.Forbidden("Only administrators can manage accounts.");

    /// <summary>Reset password, deactivate and reactivate: an Admin anyone; with the tick, others the people below their own
    /// type in their department.</summary>
    private async Task<AppError?> RequireAccountManagerAsync(User user, CancellationToken ct)
    {
        var rights = await RightsAsync(ct);
        if (rights.IsAdministrator)
        {
            return null;
        }

        if (!rights.CanManageAccounts)
        {
            return AppError.Forbidden("You don't have the \"Reset passwords and deactivate users\" right. Ask an Admin.");
        }

        return rights.Manages(TypeOf(user.OrganizationRole, user.UserType), user.DepartmentId)
            ? null
            : AppError.Forbidden("You can only manage people below your own type in your department.");
    }
}
