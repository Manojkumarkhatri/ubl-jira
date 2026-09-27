using Upms.Application.Common.Results;
using Upms.Application.Identity;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeAccountService : IAccountService
{
    /// <summary>The viewer's time zone (IANA ID).</summary>
    public string TimeZoneId { get; set; } = "UTC";

    public Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct) =>
        Task.FromResult(Result<MyProfile>.Ok(new MyProfile(Guid.NewGuid(), "amina", "Amina Khan", "amina@example.com", null, TimeZoneId, false)));

    public Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct) => Task.FromResult(Result.Ok());

    public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct) => Task.FromResult(Result.Ok());
}
