using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Minimal account management (FR-003, FR-004, FR-010).</summary>
public sealed class UserAdminServiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _admin = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _admin = await Data.AdministratorAsync();
        ActAs(_admin);
    }

    private Task<Result<CreatedUser>> AddAsync(string userName, string? email = null) =>
        CallAsync<IUserAdminService, Result<CreatedUser>>(s =>
            s.AddUserAsync(userName, "Amina Khan", email ?? $"{userName}@example.com", Ct));

    private async Task<bool> PasswordWorksAsync(Guid userId, string password)
    {
        await using var scope = Harness.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return await userManager.CheckPasswordAsync(user!, password);
    }

    [Fact]
    public async Task Adding_a_user_returns_a_16_character_temporary_password_once()
    {
        var result = await AddAsync("amina");

        Assert.True(result.IsSuccess, result.Error?.Message);
        var created = result.Value!;
        Assert.Equal(16, created.TemporaryPassword.Length);
        Assert.True(await PasswordWorksAsync(created.Id, created.TemporaryPassword));

        var user = await ReloadUserAsync(created.Id);
        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.Equal(OrganizationRole.User, user.OrganizationRole);
        Assert.Equal("Amina Khan", user.DisplayName);

        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.UserCreated);
        Assert.Equal(_admin.Id, audit.ActorUserId);
        Assert.Equal(created.Id, audit.SubjectUserId);
        Assert.DoesNotContain(created.TemporaryPassword, audit.Details ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_user_names_and_emails_are_refused()
    {
        await AddAsync("amina");

        Assert.Equal(ErrorCodes.DuplicateUserName, (await AddAsync("AMINA", "other@example.com")).Error!.Code);
        Assert.Equal(ErrorCodes.DuplicateEmail, (await AddAsync("bilal", "amina@example.com")).Error!.Code);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task Invalid_user_names_are_refused(string userName)
    {
        Assert.Equal(ErrorKind.Validation, (await AddAsync(userName, "valid@example.com")).Error!.Kind);
    }

    [Fact]
    public async Task Resetting_a_password_issues_a_new_temporary_password()
    {
        var created = (await AddAsync("amina")).Value!;

        var reset = await CallAsync<IUserAdminService, Result<string>>(s => s.ResetPasswordAsync(created.Id, Ct));

        Assert.True(reset.IsSuccess, reset.Error?.Message);
        Assert.NotEqual(created.TemporaryPassword, reset.Value);
        Assert.True(await PasswordWorksAsync(created.Id, reset.Value!));
        Assert.False(await PasswordWorksAsync(created.Id, created.TemporaryPassword));
        Assert.True((await ReloadUserAsync(created.Id)).MustChangePassword);
        Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.PasswordReset);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_rotate_the_security_stamp_and_are_audited()
    {
        var created = (await AddAsync("amina")).Value!;
        var stampBefore = (await ReloadUserAsync(created.Id)).SecurityStamp;

        var deactivated = await CallAsync<IUserAdminService, Result>(s => s.DeactivateAsync(created.Id, Ct));

        Assert.True(deactivated.IsSuccess, deactivated.Error?.Message);
        var user = await ReloadUserAsync(created.Id);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeactivatedAt);
        Assert.NotEqual(stampBefore, user.SecurityStamp);

        var reactivated = await CallAsync<IUserAdminService, Result>(s => s.ReactivateAsync(created.Id, Ct));

        Assert.True(reactivated.IsSuccess);
        Assert.True((await ReloadUserAsync(created.Id)).IsActive);
        var types = (await AuditEventsAsync()).Select(e => e.EventType).ToList();
        Assert.Contains(AuditEventType.UserDeactivated, types);
        Assert.Contains(AuditEventType.UserReactivated, types);
    }

    [Fact]
    public async Task The_last_active_administrator_cannot_be_deactivated()
    {
        var result = await CallAsync<IUserAdminService, Result>(s => s.DeactivateAsync(_admin.Id, Ct));

        Assert.Equal(ErrorCodes.LastAdministrator, result.Error!.Code);
        Assert.True((await ReloadUserAsync(_admin.Id)).IsActive);
    }

    [Fact]
    public async Task Users_are_listed_with_search_and_paging()
    {
        foreach (var name in new[] { "amina", "bilal", "carla" })
        {
            await AddAsync(name);
        }

        var all = await CallAsync<IUserAdminService, Result<Page<UserSummary>>>(s =>
            s.ListUsersAsync(null, new PageRequest(1, 2), Ct));
        var found = await CallAsync<IUserAdminService, Result<Page<UserSummary>>>(s =>
            s.ListUsersAsync("bil", PageRequest.First, Ct));

        Assert.Equal(4, all.Value!.TotalCount);
        Assert.Equal(2, all.Value.Items.Count);
        Assert.Equal("admin", all.Value.Items[0].UserName);
        Assert.Equal("bilal", Assert.Single(found.Value!.Items).UserName);
    }

    [Fact]
    public async Task Non_administrators_are_forbidden()
    {
        var amina = await Data.UserAsync("amina");
        ActAs(amina);

        Assert.Equal(ErrorKind.Forbidden, (await AddAsync("bilal")).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IUserAdminService, Result<Page<UserSummary>>>(s =>
            s.ListUsersAsync(null, PageRequest.First, Ct))).Error!.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await CallAsync<IUserAdminService, Result>(s =>
            s.DeactivateAsync(_admin.Id, Ct))).Error!.Kind);
    }
}
