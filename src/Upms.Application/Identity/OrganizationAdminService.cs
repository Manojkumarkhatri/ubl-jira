using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Identity;

internal sealed class OrganizationAdminService(
    IAppDbContext db,
    ICallerContext caller,
    IAuditLog auditLog,
    ISpaceAccessSync spaceAccess,
    TimeProvider time) : IOrganizationAdminService
{
    public const string DepartmentInUseCode = "DepartmentInUse";

    public async Task<IReadOnlyList<DepartmentInfo>> DepartmentsAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true })
        {
            return [];
        }

        return (await db.Departments.AsNoTracking()
                .Select(d => new { d.Id, d.Name, People = db.Users.Count(u => u.DepartmentId == d.Id) })
                .ToListAsync(ct))
            .Select(d => new DepartmentInfo(d.Id, d.Name, d.People))
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Result<long>> CreateDepartmentAsync(string name, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is { } denied)
        {
            return denied;
        }

        var created = Department.Create(name, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        if (await db.Departments.AnyAsync(d => d.NormalizedName == created.Value!.NormalizedName, ct))
        {
            return Duplicate();
        }

        db.Departments.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        await auditLog.WriteAsync(AuditEventType.DepartmentChanged, null, created.Value!.Name, new { Added = created.Value.Name }, ct);
        return created.Value.Id;
    }

    public async Task<Result> RenameDepartmentAsync(long departmentId, string name, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is { } denied)
        {
            return denied;
        }

        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, ct);
        if (department is null)
        {
            return AppError.NotFound("department");
        }

        var before = department.Name;
        if (department.Rename(name) is { } refused)
        {
            return refused.ToAppError();
        }

        if (await db.Departments.AnyAsync(d => d.Id != departmentId && d.NormalizedName == department.NormalizedName, ct))
        {
            return Duplicate();
        }

        await db.SaveChangesAsync(ct);
        await auditLog.WriteAsync(AuditEventType.DepartmentChanged, null, department.Name, new { From = before, To = department.Name }, ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteDepartmentAsync(long departmentId, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is { } denied)
        {
            return denied;
        }

        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, ct);
        if (department is null)
        {
            return AppError.NotFound("department");
        }

        if (await db.Users.AnyAsync(u => u.DepartmentId == departmentId, ct))
        {
            return InUse();
        }

        db.Departments.Remove(department);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Its portfolios still point at it (they belong to the Projects module).
            db.Departments.Entry(department).State = EntityState.Unchanged;
            return InUse();
        }

        await auditLog.WriteAsync(AuditEventType.DepartmentChanged, null, department.Name, new { Deleted = department.Name }, ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyDictionary<UserType, Rights>> TypeRightsAsync(CancellationToken ct)
    {
        var stored = await db.UserTypeRights.AsNoTracking().ToDictionaryAsync(r => r.Type, r => r.Rights, ct);
        return UserTypes.Ordered.ToDictionary(t => t,
            t => t == UserType.Admin ? Rights.All : stored.TryGetValue(t, out var r) ? r : RightsCatalog.Defaults(t));
    }

    public async Task<Result> SetTypeRightsAsync(UserType type, Rights rights, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is { } denied)
        {
            return denied;
        }

        if (!Enum.IsDefined(type))
        {
            return AppError.Validation("Type", "Choose a type.");
        }

        if (type == UserType.Admin)
        {
            return Result.Ok();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await db.UserTypeRights.SingleOrDefaultAsync(r => r.Type == type, ct);
        if (row is null)
        {
            row = new UserTypeRights(type, RightsCatalog.Defaults(type));
            db.UserTypeRights.Add(row);
        }

        var before = row.Rights;
        row.Set(rights);
        if (row.Rights == before)
        {
            return Result.Ok();
        }

        await db.SaveChangesAsync(ct);
        await auditLog.WriteAsync(AuditEventType.TypeRightsChanged, null, type.ToString(),
            new { From = before.ToString(), To = row.Rights.ToString() }, ct);
        var people = await db.Users.Where(u => u.UserType == type && u.OrganizationRole != OrganizationRole.Administrator)
            .Select(u => u.Id).ToListAsync(ct);
        await spaceAccess.SyncPeopleAsync(people, ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<MyAccess> MyAccessAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return MyAccess.None;
        }

        var department = me.DepartmentId is { } id
            ? await db.Departments.AsNoTracking().Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync(ct)
            : null;
        return new MyAccess(me.Type, department, me.Rights, me.IsAdministrator);
    }

    private async Task<AppError?> RequireAdminAsync(CancellationToken ct) =>
        await caller.GetAsync(ct) is { IsActive: true, IsAdministrator: true }
            ? null
            : AppError.Forbidden("Only Admins can change departments and rights.");

    private static AppError Duplicate() => AppError.Validation("Name", "There is already a department with this name.");

    private static AppError InUse() => AppError.Rule(DepartmentInUseCode,
        "This department still has people or portfolios. Move them to another department first.");
}
