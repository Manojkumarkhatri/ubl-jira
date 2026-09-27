using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Domain.Identity;

namespace Upms.Web.Tests.Fakes;

/// <summary>In-memory accounts for the Accounts page; records role changes.</summary>
public sealed class FakeUserAdminService : IUserAdminService
{
    public List<UserSummary> Users { get; } = [];

    public List<string> Calls { get; } = [];

    /// <summary>Returned instead of applying the next change.</summary>
    public AppError? NextError { get; set; }

    public UserSummary Add(string userName, OrganizationRole role = OrganizationRole.User, bool isActive = true, Guid? id = null)
    {
        var user = new UserSummary(id ?? Guid.NewGuid(), userName, $"{userName} Tester", $"{userName}@example.com", role, isActive,
            IsLockedOut: false, CreatedAt: new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), LastSignInAt: null);
        Users.Add(user);
        return user;
    }

    public Task<Result<Page<UserSummary>>> ListUsersAsync(string? search, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<UserSummary>>.Ok(new Page<UserSummary>(Users.ToList(), Users.Count, 1, PageRequest.DefaultPageSize)));

    public Task<Result<CreatedUser>> AddUserAsync(string userName, string displayName, string email, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<string>> ResetPasswordAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result> DeactivateAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result> ReactivateAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result> ChangeRoleAsync(Guid userId, OrganizationRole role, CancellationToken ct)
    {
        Calls.Add($"role {Users.Single(u => u.Id == userId).UserName} {role}");
        if (NextError is { } error)
        {
            NextError = null;
            return Task.FromResult(Result.Fail(error));
        }

        var index = Users.FindIndex(u => u.Id == userId);
        Users[index] = Users[index] with { Role = role };
        return Task.FromResult(Result.Ok());
    }
}
