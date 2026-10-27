using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Domain.Common;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeAccountService : IAccountService
{
    /// <summary>The viewer's time zone (IANA ID).</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>The viewer's email choice (Phase 4 FR-051).</summary>
    public EmailChoice EmailChoice { get; set; } = EmailChoice.AimedAtMe;

    public List<EmailChoice> ChoicesSet { get; } = [];

    public bool IsAdministrator { get; set; }

    public bool SidebarCollapsed { get; set; }

    public List<bool> SidebarChoices { get; } = [];

    public Task<Result<MyProfile>> GetProfileAsync(CancellationToken ct) =>
        Task.FromResult(Result<MyProfile>.Ok(new MyProfile(Guid.NewGuid(), "amina", "Amina Khan", "amina@example.com", null, TimeZoneId, IsAdministrator,
            EmailChoice, SidebarCollapsed)));

    public Task<Result> UpdateProfileAsync(string displayName, string? timeZoneId, CancellationToken ct) => Task.FromResult(Result.Ok());

    public Task<Result> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct) => Task.FromResult(Result.Ok());

    public Task<Result<MyProfile>> SetEmailChoiceAsync(EmailChoice choice, CancellationToken ct)
    {
        ChoicesSet.Add(choice);
        EmailChoice = choice;
        return GetProfileAsync(ct);
    }

    public Task<Result<MyProfile>> SetSidebarCollapsedAsync(bool collapsed, CancellationToken ct)
    {
        SidebarChoices.Add(collapsed);
        SidebarCollapsed = collapsed;
        return GetProfileAsync(ct);
    }
}
