using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Upms.Application.Identity;
using Upms.Web.Components.Shared;
using Upms.Web.Security;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Security;

/// <summary>The idle-session warning (FR-006): 30-minute timeout, warning 2 minutes before.</summary>
public sealed class IdleSessionWarningTests : BunitTestBase
{
    private readonly FakeSessionActivity _activity;

    public IdleSessionWarningTests()
    {
        _activity = new FakeSessionActivity(Time);
        Services.AddSingleton<ISessionActivity>(_activity);
        Services.AddSingleton<IOrganizationSettingsReader>(new FakeOrganizationSettingsReader(idleTimeoutMinutes: 30));
        JSInterop.Setup<bool>("upmsIdle.keepAlive").SetResult(true);
        JSInterop.SetupVoid("upmsIdle.expire").SetVoidResult();
    }

    [Fact]
    public void No_warning_is_shown_before_28_minutes_of_inactivity()
    {
        var cut = Render<IdleSessionWarning>();

        Time.Advance(TimeSpan.FromMinutes(27.5));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=idle-warning]")));
    }

    [Fact]
    public void The_warning_appears_2_minutes_before_the_timeout()
    {
        var cut = Render<IdleSessionWarning>();

        Time.Advance(TimeSpan.FromMinutes(28));

        cut.WaitForAssertion(() =>
        {
            var warning = cut.Find("[data-testid=idle-warning]");
            Assert.Equal("alertdialog", warning.GetAttribute("role"));
            Assert.Contains("Stay signed in", warning.TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Activity_postpones_the_warning()
    {
        var cut = Render<IdleSessionWarning>();

        Time.Advance(TimeSpan.FromMinutes(20));
        _activity.Touch();
        Time.Advance(TimeSpan.FromMinutes(20));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=idle-warning]")));
    }

    [Fact]
    public void Stay_signed_in_renews_the_session_and_hides_the_warning()
    {
        var cut = Render<IdleSessionWarning>();
        Time.Advance(TimeSpan.FromMinutes(28.5));
        cut.WaitForElement("[data-testid=idle-warning]");
        var touchesBefore = _activity.TouchCount;

        cut.Find("[data-testid=stay-signed-in]").Click();

        JSInterop.VerifyInvoke("upmsIdle.keepAlive");
        Assert.Equal(touchesBefore + 1, _activity.TouchCount);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=idle-warning]")));
    }

    [Fact]
    public void At_the_timeout_the_session_ends_and_the_user_is_signed_out()
    {
        var cut = Render<IdleSessionWarning>();

        Time.Advance(TimeSpan.FromMinutes(30));

        cut.WaitForAssertion(() => JSInterop.VerifyInvoke("upmsIdle.expire"));
        Assert.True(_activity.IsExpired);
    }

    [Fact]
    public void When_the_browser_cannot_sign_out_the_user_is_sent_to_sign_in()
    {
        JSInterop.SetupVoid("upmsIdle.expire").SetException(new JSException("disconnected"));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<IdleSessionWarning>();

        Time.Advance(TimeSpan.FromMinutes(30));

        cut.WaitForAssertion(() => Assert.EndsWith("/Account/Login?expired=1", navigation.Uri, StringComparison.Ordinal));
    }
}
