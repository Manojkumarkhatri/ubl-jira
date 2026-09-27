using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Minimal account management (FR-003, FR-004, FR-008, FR-010).</summary>
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

    private Task<Result> ChangeRoleAsync(Guid userId, OrganizationRole role) =>
        CallAsync<IUserAdminService, Result>(s => s.ChangeRoleAsync(userId, role, Ct));

    /// <summary>A call from <paramref name="caller"/> on its own thread, for calls made at the same moment.</summary>
    private Task<Result> InParallelAs(User caller, Func<IUserAdminService, Task<Result>> call) => Task.Run(async () =>
    {
        Harness.CurrentUser.UseInThisFlow(caller.Id);
        return await CallAsync(call);
    });

    private async Task<bool> CanManageAccountsAsync(User user)
    {
        ActAs(user);
        var result = await CallAsync<IUserAdminService, Result<Page<UserSummary>>>(s => s.ListUsersAsync(null, PageRequest.First, Ct));
        return result.IsSuccess;
    }

    private Task<int> ActiveAdministratorCountAsync() =>
        QueryAsync(db => db.Users.CountAsync(u => u.IsActive && u.OrganizationRole == OrganizationRole.Administrator, Ct));

    private Task<int> ResetToActiveAdministratorsAsync(params User[] users)
    {
        var ids = users.Select(u => u.Id).ToList();
        return QueryAsync(db => db.Users.Where(u => ids.Contains(u.Id)).ExecuteUpdateAsync(set => set
            .SetProperty(u => u.OrganizationRole, OrganizationRole.Administrator)
            .SetProperty(u => u.IsActive, true)
            .SetProperty(u => u.DeactivatedAt, (DateTimeOffset?)null), Ct));
    }

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
    public async Task FR008_A_colleague_made_an_administrator_can_manage_accounts_at_once_and_the_change_is_audited()
    {
        var amina = await Data.UserAsync("amina");
        var stampBefore = amina.SecurityStamp;

        var result = await ChangeRoleAsync(amina.Id, OrganizationRole.Administrator);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var reloaded = await ReloadUserAsync(amina.Id);
        Assert.Equal(OrganizationRole.Administrator, reloaded.OrganizationRole);
        Assert.Equal(stampBefore, reloaded.SecurityStamp); // not signed out: rights are read on every call
        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.RoleChanged);
        Assert.Equal(_admin.Id, audit.ActorUserId);
        Assert.Equal(amina.Id, audit.SubjectUserId);
        Assert.Equal("amina", audit.Target);
        Assert.Equal("""{"From":"User","To":"Administrator"}""", audit.Details);
        Assert.True(await CanManageAccountsAsync(amina));
    }

    [Fact]
    public async Task FR008_Removing_the_role_applies_to_the_persons_next_call_and_is_audited()
    {
        var amina = await Data.AdministratorAsync("amina");

        var result = await ChangeRoleAsync(amina.Id, OrganizationRole.User);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(OrganizationRole.User, (await ReloadUserAsync(amina.Id)).OrganizationRole);
        var audit = Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.RoleChanged);
        Assert.Equal("""{"From":"Administrator","To":"User"}""", audit.Details);
        Assert.False(await CanManageAccountsAsync(amina));
    }

    [Fact]
    public async Task FR008_An_administrator_can_hand_over_the_role_and_then_remove_their_own()
    {
        var amina = await Data.UserAsync("amina");
        Assert.True((await ChangeRoleAsync(amina.Id, OrganizationRole.Administrator)).IsSuccess);

        var result = await ChangeRoleAsync(_admin.Id, OrganizationRole.User);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await CanManageAccountsAsync(_admin));
        Assert.True(await CanManageAccountsAsync(amina));
    }

    [Fact]
    public async Task FR008_The_last_active_administrator_keeps_the_role()
    {
        var result = await ChangeRoleAsync(_admin.Id, OrganizationRole.User);

        Assert.Equal(ErrorCodes.LastAdministrator, result.Error!.Code);
        Assert.Equal(OrganizationRole.Administrator, (await ReloadUserAsync(_admin.Id)).OrganizationRole);
        Assert.DoesNotContain(await AuditEventsAsync(), e => e.EventType == AuditEventType.RoleChanged);
    }

    [Fact]
    public async Task FR008_A_deactivated_administrator_does_not_count_and_can_lose_the_role()
    {
        var dora = await Data.AdministratorAsync("dora");
        Assert.True((await CallAsync<IUserAdminService, Result>(s => s.DeactivateAsync(dora.Id, Ct))).IsSuccess);

        Assert.Equal(ErrorCodes.LastAdministrator, (await ChangeRoleAsync(_admin.Id, OrganizationRole.User)).Error!.Code);
        Assert.Equal(ErrorCodes.LastAdministrator, (await CallAsync<IUserAdminService, Result>(s =>
            s.DeactivateAsync(_admin.Id, Ct))).Error!.Code);
        Assert.True((await ChangeRoleAsync(dora.Id, OrganizationRole.User)).IsSuccess);
        Assert.Equal(OrganizationRole.User, (await ReloadUserAsync(dora.Id)).OrganizationRole);
    }

    [Fact]
    public async Task FR008_Only_an_active_account_can_become_an_administrator()
    {
        var dora = await Data.UserAsync("dora", isActive: false);

        var result = await ChangeRoleAsync(dora.Id, OrganizationRole.Administrator);

        Assert.Equal(ErrorCodes.AccountDeactivated, result.Error!.Code);
        Assert.Equal(OrganizationRole.User, (await ReloadUserAsync(dora.Id)).OrganizationRole);
    }

    [Fact]
    public async Task Giving_the_role_someone_already_has_changes_nothing_and_unknown_accounts_are_not_found()
    {
        Assert.True((await ChangeRoleAsync(_admin.Id, OrganizationRole.Administrator)).IsSuccess);
        Assert.DoesNotContain(await AuditEventsAsync(), e => e.EventType == AuditEventType.RoleChanged);

        Assert.Equal(ErrorKind.NotFound, (await ChangeRoleAsync(Guid.NewGuid(), OrganizationRole.Administrator)).Error!.Kind);
        Assert.Equal(ErrorKind.Validation, (await ChangeRoleAsync(_admin.Id, (OrganizationRole)7)).Error!.Kind);
    }

    [Fact]
    public async Task FR008_Two_administrators_removing_each_other_at_the_same_moment_leave_one_administrator()
    {
        var amina = await Data.AdministratorAsync("amina");

        for (var round = 0; round < 5; round++)
        {
            await ResetToActiveAdministratorsAsync(_admin, amina);

            var results = await Task.WhenAll(
                InParallelAs(_admin, s => s.ChangeRoleAsync(amina.Id, OrganizationRole.User, Ct)),
                InParallelAs(amina, s => s.ChangeRoleAsync(_admin.Id, OrganizationRole.User, Ct)));

            // The second change waits for the first, then finds that its caller is no longer an administrator.
            Assert.Single(results, r => r.IsSuccess);
            Assert.Single(results, r => r.Error?.Kind == ErrorKind.Forbidden);
            Assert.Equal(1, await ActiveAdministratorCountAsync());
        }
    }

    [Fact]
    public async Task FR008_Deactivating_and_removing_the_role_at_the_same_moment_leave_an_active_administrator()
    {
        var amina = await Data.AdministratorAsync("amina");

        for (var round = 0; round < 5; round++)
        {
            await ResetToActiveAdministratorsAsync(_admin, amina);

            var results = await Task.WhenAll(
                InParallelAs(_admin, s => s.DeactivateAsync(amina.Id, Ct)),
                InParallelAs(amina, s => s.ChangeRoleAsync(_admin.Id, OrganizationRole.User, Ct)));

            Assert.Single(results, r => r.IsSuccess);
            Assert.Equal(1, await ActiveAdministratorCountAsync());
        }
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
        Assert.Equal(ErrorKind.Forbidden, (await ChangeRoleAsync(amina.Id, OrganizationRole.Administrator)).Error!.Kind);
        Assert.Equal(OrganizationRole.User, (await ReloadUserAsync(amina.Id)).OrganizationRole);
    }
}
