using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Password policy, lockout and sign-in auditing (FR-001, FR-005, FR-010).</summary>
public sealed class SignInPolicyTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private async Task<IdentityResult> CreateWithPasswordAsync(string userName, string password)
    {
        await using var scope = Harness.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        return await userManager.CreateAsync(
            new User { UserName = userName, Email = $"{userName}@example.com", DisplayName = userName }, password);
    }

    private async Task<SignInResult> SignInAsync(string userName, string password)
    {
        await using var scope = Harness.CreateScope();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        return await signInManager.PasswordSignInAsync(userName, password, isPersistent: false, lockoutOnFailure: true);
    }

    [Fact]
    public async Task Passwords_shorter_than_12_characters_are_rejected()
    {
        var result = await CreateWithPasswordAsync("amina", "elevenchars");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.PasswordTooShort));
    }

    [Fact]
    public async Task Passwords_need_no_character_classes_once_long_enough()
    {
        Assert.True((await CreateWithPasswordAsync("amina", "all lowercase words")).Succeeded);
    }

    [Theory]
    [InlineData("my amina password")]
    [InlineData("my AMINA password")]
    public async Task Passwords_containing_the_user_name_are_rejected(string password)
    {
        var result = await CreateWithPasswordAsync("amina", password);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordContainsUserName");
    }

    [Fact]
    public async Task A_correct_password_signs_in_and_is_audited_without_the_password()
    {
        var user = await Data.UserAsync("amina");

        var result = await SignInAsync("amina", TestData.DefaultPassword);

        Assert.True(result.Succeeded);
        var audit = Assert.Single(await AuditEventsAsync());
        Assert.Equal(AuditEventType.SignInSucceeded, audit.EventType);
        Assert.Equal(user.Id, audit.ActorUserId);
        Assert.DoesNotContain(TestData.DefaultPassword, audit.Details ?? "", StringComparison.Ordinal);
        Assert.NotNull((await ReloadUserAsync(user.Id)).LastSignInAt);
    }

    [Fact]
    public async Task Failed_sign_ins_are_audited_including_unknown_user_names()
    {
        await Data.UserAsync("amina");

        Assert.False((await SignInAsync("amina", "wrong password here")).Succeeded);
        Assert.False((await SignInAsync("nobody", "wrong password here")).Succeeded);

        var events = await AuditEventsAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(AuditEventType.SignInFailed, e.EventType));
        Assert.Contains(events, e => e.Target == "nobody" && e.ActorUserId is null);
        Assert.All(events, e => Assert.DoesNotContain("wrong password here", e.Details ?? "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Five_consecutive_failures_lock_the_account_for_15_minutes()
    {
        var user = await Data.UserAsync("amina");

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.False((await SignInAsync("amina", "wrong password here")).IsLockedOut);
        }

        Assert.True((await SignInAsync("amina", "wrong password here")).IsLockedOut);
        Assert.True((await SignInAsync("amina", TestData.DefaultPassword)).IsLockedOut);

        var lockoutEnd = (await ReloadUserAsync(user.Id)).LockoutEnd;
        Assert.NotNull(lockoutEnd);
        var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
        Assert.InRange(remaining, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));
        Assert.Single(await AuditEventsAsync(), e => e.EventType == AuditEventType.LockedOut);
    }

    [Fact]
    public async Task Deactivated_users_cannot_sign_in()
    {
        await Data.UserAsync("amina", isActive: false);

        var result = await SignInAsync("amina", TestData.DefaultPassword);

        Assert.False(result.Succeeded);
        Assert.True(result.IsNotAllowed);
        Assert.Contains(await AuditEventsAsync(), e => e.EventType == AuditEventType.SignInFailed);
    }
}
