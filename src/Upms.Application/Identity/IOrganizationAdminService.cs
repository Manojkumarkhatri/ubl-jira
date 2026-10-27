using Upms.Application.Common.Results;
using Upms.Domain.Common;

namespace Upms.Application.Identity;

/// <summary>Departments and each user type's default ticks (owner's decision of 2026-10-20); Admins only, though everyone
/// signed in may read the department names and the ticks.</summary>
public interface IOrganizationAdminService
{
    Task<IReadOnlyList<DepartmentInfo>> DepartmentsAsync(CancellationToken ct);

    Task<Result<long>> CreateDepartmentAsync(string name, CancellationToken ct);

    Task<Result> RenameDepartmentAsync(long departmentId, string name, CancellationToken ct);

    /// <summary>Only a department with no people and no portfolios (<c>DepartmentInUse</c>).</summary>
    Task<Result> DeleteDepartmentAsync(long departmentId, CancellationToken ct);

    /// <summary>The default ticks of every type.</summary>
    Task<IReadOnlyDictionary<UserType, Rights>> TypeRightsAsync(CancellationToken ct);

    /// <summary>Sets a type's default ticks, and the roles of its people in their spaces. An Admin keeps every tick.</summary>
    Task<Result> SetTypeRightsAsync(UserType type, Rights rights, CancellationToken ct);

    /// <summary>The caller's own type, department and ticks, for what pages offer them; none when signed out.</summary>
    Task<MyAccess> MyAccessAsync(CancellationToken ct);
}

public sealed record DepartmentInfo(long Id, string Name, int People);

public sealed record MyAccess(UserType Type, string? Department, Rights Rights, bool IsAdministrator)
{
    public static readonly MyAccess None = new(UserType.ReadOnly, null, Rights.None, false);

    public bool Can(Rights right) => IsAdministrator || Rights.Has(right);
}
