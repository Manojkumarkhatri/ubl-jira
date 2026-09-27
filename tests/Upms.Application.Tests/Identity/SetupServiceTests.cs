using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>First-run setup (FR-002, research R8).</summary>
public sealed class SetupServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private const string Password = "a long first-run passphrase";

    private Task<Result<Guid>> CreateAdminAsync(string token = ServiceHarness.SetupToken, string password = Password) =>
        CallAsync<ISetupService, Result<Guid>>(s =>
            s.CreateFirstAdministratorAsync(token, "admin", "Ada Admin", "admin@example.com", password, Ct));

    [Fact]
    public async Task Setup_is_open_on_a_fresh_installation()
    {
        Assert.True(await CallAsync<ISetupService, bool>(s => s.IsSetupOpenAsync(Ct)));
    }

    [Fact]
    public async Task A_wrong_setup_token_is_refused_and_setup_stays_open()
    {
        var result = await CreateAdminAsync(token: "not-the-token");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidSetupToken, result.Error!.Code);
        Assert.True(await CallAsync<ISetupService, bool>(s => s.IsSetupOpenAsync(Ct)));
    }

    [Fact]
    public async Task Setup_creates_an_active_administrator_then_closes_for_good()
    {
        var result = await CreateAdminAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        var admin = await ReloadUserAsync(result.Value);
        Assert.Equal("admin", admin.UserName);
        Assert.Equal("Ada Admin", admin.DisplayName);
        Assert.Equal(OrganizationRole.Administrator, admin.OrganizationRole);
        Assert.True(admin.IsActive);
        Assert.False(admin.MustChangePassword);

        Assert.False(await CallAsync<ISetupService, bool>(s => s.IsSetupOpenAsync(Ct)));
        var second = await CreateAdminAsync();
        Assert.Equal(ErrorCodes.SetupClosed, second.Error!.Code);

        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.SetupCompleted);
        Assert.Equal(admin.Id, audit.SubjectUserId);
        Assert.DoesNotContain(Password, audit.Details ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Setup_is_closed_while_an_active_administrator_exists()
    {
        await Data.AdministratorAsync("existing");

        Assert.False(await CallAsync<ISetupService, bool>(s => s.IsSetupOpenAsync(Ct)));
        Assert.Equal(ErrorCodes.SetupClosed, (await CreateAdminAsync()).Error!.Code);
    }

    [Fact]
    public async Task The_administrator_password_must_meet_the_policy()
    {
        var result = await CreateAdminAsync(password: "too short");

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.True(await CallAsync<ISetupService, bool>(s => s.IsSetupOpenAsync(Ct)));
    }
}
