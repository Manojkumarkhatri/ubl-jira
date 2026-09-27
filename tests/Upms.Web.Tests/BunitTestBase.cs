using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Upms.Application.Common;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests;

/// <summary>Base for component tests (tasks.md T049): a signed-in user, a fake clock, loose JS interop
/// and fake application services that each test configures.</summary>
public abstract class BunitTestBase : BunitContext
{
    protected static readonly DateTimeOffset Start = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    protected BunitTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Time = new FakeTimeProvider(Start);
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<ICurrentUser>(CurrentUser);
        // The viewer's time zone and today (UTC unless a test registers another account service).
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
        Services.AddScoped<Upms.Web.Components.Shared.ViewerTimeZone>();
        Services.AddScoped<Upms.Web.Components.Shared.ViewerToday>();
        Auth = AddAuthorization();
        Auth.SetAuthorized("amina");
        Auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUser.UserId!.Value.ToString()));
    }

    protected FakeTimeProvider Time { get; }

    protected BunitAuthorizationContext Auth { get; }

    protected FakeCurrentUser CurrentUser { get; } = new() { UserId = Guid.NewGuid() };

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;
}
