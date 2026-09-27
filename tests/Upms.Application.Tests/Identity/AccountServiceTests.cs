using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Identity.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>The user's own profile and password (FR-007, FR-043) and display names for other modules.</summary>
public sealed class AccountServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _amina = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _amina = await Data.UserAsync("amina");
        ActAs(_amina);
    }

    private Task<Result> UpdateAsync(string displayName, string? timeZoneId) =>
        CallAsync<IAccountService, Result>(s => s.UpdateProfileAsync(displayName, timeZoneId, Ct));

    [Fact]
    public async Task The_profile_uses_the_organization_time_zone_by_default()
    {
        var profile = (await CallAsync<IAccountService, Result<MyProfile>>(s => s.GetProfileAsync(Ct))).Value!;

        Assert.Equal("amina", profile.UserName);
        Assert.Null(profile.TimeZoneId);
        Assert.Equal("UTC", profile.EffectiveTimeZoneId);
    }

    [Fact]
    public async Task Display_name_and_time_zone_can_be_changed()
    {
        var result = await UpdateAsync("  Amina K.  ", "Asia/Karachi");

        Assert.True(result.IsSuccess, result.Error?.Message);
        var user = await ReloadUserAsync(_amina.Id);
        Assert.Equal("Amina K.", user.DisplayName);
        Assert.Equal("Asia/Karachi", user.TimeZoneId);

        var profile = (await CallAsync<IAccountService, Result<MyProfile>>(s => s.GetProfileAsync(Ct))).Value!;
        Assert.Equal("Asia/Karachi", profile.EffectiveTimeZoneId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task The_display_name_is_required(string displayName)
    {
        var result = await UpdateAsync(displayName, null);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("DisplayName", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task The_display_name_has_at_most_100_characters()
    {
        Assert.True((await UpdateAsync(new string('a', 100), null)).IsSuccess);
        Assert.Equal(ErrorKind.Validation, (await UpdateAsync(new string('a', 101), null)).Error!.Kind);
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("not a zone")]
    public async Task Unknown_time_zones_are_refused(string timeZoneId)
    {
        var result = await UpdateAsync("Amina", timeZoneId);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("TimeZoneId", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Clearing_the_time_zone_returns_to_the_organization_default()
    {
        await UpdateAsync("Amina", "Asia/Karachi");

        Assert.True((await UpdateAsync("Amina", null)).IsSuccess);
        Assert.Null((await ReloadUserAsync(_amina.Id)).TimeZoneId);
    }

    [Fact]
    public async Task Changing_the_password_enforces_the_policy_and_is_audited()
    {
        var tooShort = await CallAsync<IAccountService, Result>(s =>
            s.ChangePasswordAsync(TestData.DefaultPassword, "short", Ct));
        Assert.Equal(ErrorKind.Validation, tooShort.Error!.Kind);

        var wrongCurrent = await CallAsync<IAccountService, Result>(s =>
            s.ChangePasswordAsync("not my password at all", "a brand new passphrase", Ct));
        Assert.Equal(ErrorKind.Validation, wrongCurrent.Error!.Kind);

        var ok = await CallAsync<IAccountService, Result>(s =>
            s.ChangePasswordAsync(TestData.DefaultPassword, "a brand new passphrase", Ct));
        Assert.True(ok.IsSuccess, ok.Error?.Message);

        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.PasswordChanged);
        Assert.Equal(_amina.Id, audit.SubjectUserId);
        Assert.DoesNotContain("a brand new passphrase", audit.Details ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_a_temporary_password_clears_the_forced_change()
    {
        var temp = await Data.UserAsync("bilal", mustChangePassword: true);
        ActAs(temp);

        var result = await CallAsync<IAccountService, Result>(s =>
            s.ChangePasswordAsync(TestData.DefaultPassword, "my freshly chosen secret", Ct));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False((await ReloadUserAsync(temp.Id)).MustChangePassword);
    }

    [Fact]
    public async Task The_user_directory_returns_names_including_deactivated_users()
    {
        var gone = await Data.UserAsync("gone", isActive: false);

        var names = await CallAsync<IUserDirectory, IReadOnlyDictionary<Guid, UserDisplay>>(d =>
            d.GetAsync([_amina.Id, gone.Id, Guid.NewGuid()], Ct));

        Assert.Equal(2, names.Count);
        Assert.Equal(_amina.DisplayName, names[_amina.Id].DisplayName);
        Assert.False(names[gone.Id].IsActive);
    }
}
