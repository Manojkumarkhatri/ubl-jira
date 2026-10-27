using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Domain.Common;

namespace Upms.Web.Tests.Fakes;

/// <summary>Departments, type ticks and the viewer's own access; an Admin unless a test says otherwise.</summary>
public sealed class FakeOrganizationAdminService : IOrganizationAdminService
{
    public List<DepartmentInfo> Departments { get; } = [new(1, "IT-PMO", 3), new(2, "Finance", 0)];

    public MyAccess Access { get; set; } = new(UserType.Admin, null, Rights.All, true);

    public Dictionary<UserType, Rights> TypeRights { get; } = UserTypes.Ordered.ToDictionary(t => t, RightsCatalog.Defaults);

    public Task<IReadOnlyList<DepartmentInfo>> DepartmentsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DepartmentInfo>>(Departments.ToList());

    public Task<Result<long>> CreateDepartmentAsync(string name, CancellationToken ct)
    {
        var id = Departments.Count == 0 ? 1 : Departments.Max(d => d.Id) + 1;
        Departments.Add(new DepartmentInfo(id, name.Trim(), 0));
        return Task.FromResult(Result<long>.Ok(id));
    }

    public Task<Result> RenameDepartmentAsync(long departmentId, string name, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result> DeleteDepartmentAsync(long departmentId, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyDictionary<UserType, Rights>> TypeRightsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<UserType, Rights>>(TypeRights.ToDictionary());

    public Task<Result> SetTypeRightsAsync(UserType type, Rights rights, CancellationToken ct)
    {
        TypeRights[type] = type == UserType.Admin ? Rights.All : rights;
        return Task.FromResult(Result.Ok());
    }

    public Task<MyAccess> MyAccessAsync(CancellationToken ct) => Task.FromResult(Access);
}
