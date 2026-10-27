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
        // Phase 4: "@" suggestions and watchers, which every drawer and editor uses.
        Services.AddSingleton<Upms.Application.Work.IMentionService>(new FakeMentionService());
        Services.AddSingleton<Upms.Application.Work.IWatcherService>(new FakeWatcherService(CurrentUser.UserId!.Value));
        Services.AddSingleton<Upms.Application.Work.IAttachmentService>(new FakeAttachmentService());
        Services.AddSingleton<Upms.Application.Work.ILabelService>(new FakeLabelService());
        // Jira parity pass: the header's Create button and the Create form.
        Services.AddSingleton<Upms.Application.Work.ICreateWorkService>(CreateWork);
        Services.AddScoped<Upms.Web.Components.Layout.CreateWorkLauncher>();
        Services.AddSingleton<Upms.Application.Projects.IBoardColumnService>(new FakeBoardColumnService());
        Services.AddSingleton<Upms.Application.Projects.ISpaceTrashService>(new FakeSpaceTrashService());
        Services.AddSingleton<Upms.Application.Projects.ISpaceNavigationService>(SpaceNavigation);
        Services.AddScoped<Upms.Web.Components.Layout.SidebarState>();
        // The space header's tabs, Shortcuts and messages, and the Timeline's status menu.
        Services.AddSingleton<Upms.Application.Projects.ISpaceViewService>(new FakeSpaceViewService());
        Services.AddScoped<Upms.Web.Components.Shared.LiveAnnouncer>();
        Services.AddSingleton<Upms.Application.Work.IWorkItemService>(new FakeWorkItemService());
        // The drawer's "⋯" menu: time tracking, votes and covers.
        Services.AddSingleton<Upms.Application.Work.IWorkItemActionService>(new FakeWorkItemActionService());
        // The five user types: the viewer's own type and ticks, which decide what pages offer.
        Services.AddSingleton<Upms.Application.Identity.IOrganizationAdminService>(Organization);
        Auth = AddAuthorization();
        Auth.SetAuthorized("amina");
        Auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUser.UserId!.Value.ToString()));
    }

    protected FakeTimeProvider Time { get; }

    protected FakeCreateWorkService CreateWork { get; } = new();

    protected FakeSpaceNavigationService SpaceNavigation { get; } = new();

    protected FakeOrganizationAdminService Organization { get; } = new();

    protected BunitAuthorizationContext Auth { get; }

    protected FakeCurrentUser CurrentUser { get; } = new() { UserId = Guid.NewGuid() };

    protected static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;
}
