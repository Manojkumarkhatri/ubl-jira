using Upms.Application.Common.Results;
using Upms.Application.Identity;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeAccountService : IAccountService
{
    public Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct) =>
        Task.FromResult(Result<MyProfile>.Ok(new MyProfile(Guid.NewGuid(), "amina", "Amina Khan", "amina@example.com", null, "UTC", false)));

    public Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct) => Task.FromResult(Result.Ok());

    public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct) => Task.FromResult(Result.Ok());
}
