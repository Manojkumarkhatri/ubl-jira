using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Upms.Application.Identity;
using Upms.Domain.Identity;
using Upms.Web.Components.Account;
using Upms.Web.Components.Shared;

namespace Upms.Web.Security;

public static class WebServiceCollectionExtensions
{
    /// <summary>Registers authentication, authorization, session handling and UI services.</summary>
    public static IServiceCollection AddUpmsWeb(this IServiceCollection services)
    {
        services.AddScoped<CurrentUser>();
        services.AddScoped<Upms.Application.Common.ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddSingleton<SessionActivityRegistry>();
        services.AddScoped<ISessionActivity, SessionActivity>();
        services.AddScoped<CircuitHandler, IdleCircuitHandler>();
        services.AddScoped<AuthenticationStateProvider, UserRevalidatingAuthenticationStateProvider>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<LiveAnnouncer>();
        services.AddScoped<Upms.Web.Components.Layout.CreateWorkLauncher>();
        services.AddScoped<Upms.Web.Components.Layout.SidebarState>();
        services.AddScoped<ViewerTimeZone>();
        services.AddScoped<ViewerToday>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            // "__Host-": the browser keeps the cookie to this exact site, over HTTPS, for all paths (ASVS 3.4.4).
            options.Cookie.Name = "__Host-upms.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/not-found";
            // The idle timeout (FR-006) plus one minute of grace for the keep-alive cadence; the exact
            // 30 minutes are enforced by the idle warning and the keep-alive endpoint.
            options.ExpireTimeSpan = TimeSpan.FromMinutes(OrganizationSettings.DefaultIdleTimeoutMinutes + 1);
            options.SlidingExpiration = true;

            // Refuse a cookie whose session has ended (signed out, idle or older than 12 hours) before the usual
            // security stamp check, so a copied cookie cannot be replayed.
            var validateSecurityStamp = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                var services = context.HttpContext.RequestServices;
                if (context.Principal is { } principal && SessionPolicy.HasEnded(principal,
                        services.GetRequiredService<SessionActivityRegistry>(), services.GetRequiredService<TimeProvider>().GetUtcNow()))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }

                await validateSecurityStamp(context);
            };
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(1);
            options.OnRefreshingPrincipal = context =>
            {
                // Keep the session ID and sign-in time across refreshes, so idle tracking and the 12-hour limit
                // survive security stamp validation.
                if (context.NewPrincipal?.Identity is ClaimsIdentity identity)
                {
                    foreach (var type in new[] { UpmsClaimTypes.SessionId, UpmsClaimTypes.SignedInAt })
                    {
                        if (context.CurrentPrincipal?.FindFirst(type) is { } kept)
                        {
                            if (identity.FindFirst(type) is { } generated)
                            {
                                identity.RemoveClaim(generated);
                            }

                            identity.AddClaim(new Claim(type, kept.Value, kept.ValueType));
                        }
                    }
                }

                return Task.CompletedTask;
            };
        });

        AuthorizationPolicies.Configure(services.AddAuthorizationBuilder());
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, UserStatusAuthorizationHandler>();
        services.AddUpmsRateLimiting();

        // UI-facing services run each call in its own scope (see OperationScope).
        services.AddOperationScoped<IAccountService>();
        services.AddOperationScoped<IUserAdminService>();
        services.AddOperationScoped<IOrganizationAdminService>();
        services.AddOperationScoped<IOrganizationSettingsReader>();
        services.AddOperationScoped<Upms.Application.Projects.IProjectService>();
        services.AddOperationScoped<Upms.Application.Projects.ISpaceTrashService>();
        services.AddOperationScoped<Upms.Application.Projects.IBoardColumnService>();
        services.AddOperationScoped<Upms.Application.Projects.IProjectMemberService>();
        services.AddOperationScoped<Upms.Application.Projects.ISpaceNavigationService>();
        services.AddOperationScoped<Upms.Application.Projects.ISpaceViewService>();
        services.AddOperationScoped<Upms.Application.Projects.IPortfolioService>();
        services.AddOperationScoped<Upms.Application.Work.IBoardService>();
        services.AddOperationScoped<Upms.Application.Work.IWorkItemService>();
        services.AddOperationScoped<Upms.Application.Work.IWorkItemActionService>();
        services.AddOperationScoped<Upms.Application.Work.ISpaceFieldService>();
        services.AddOperationScoped<Upms.Application.Work.ICreateWorkService>();
        services.AddOperationScoped<Upms.Application.Work.ISpaceSummaryService>();
        services.AddOperationScoped<Upms.Application.Work.ICommentService>();
        services.AddOperationScoped<Upms.Application.Work.IMyTasksService>();
        services.AddOperationScoped<Upms.Application.Work.IWorkItemListService>();
        services.AddOperationScoped<Upms.Application.Work.ITimelineService>();
        services.AddOperationScoped<Upms.Application.Work.ICalendarService>();
        services.AddOperationScoped<Upms.Application.Work.ISpaceReportService>();
        services.AddOperationScoped<Upms.Application.Work.IMisService>();
        services.AddOperationScoped<Upms.Application.Work.ISpaceDocService>();
        services.AddOperationScoped<Upms.Application.Work.ISpaceFormService>();
        services.AddOperationScoped<Upms.Application.Work.IBacklogService>();
        services.AddOperationScoped<Upms.Application.Work.ISprintService>();
        services.AddOperationScoped<Upms.Application.Work.IStageGateService>();
        services.AddOperationScoped<Upms.Application.Work.IDependencyService>();
        services.AddOperationScoped<Upms.Application.Work.IWatcherService>();
        services.AddOperationScoped<Upms.Application.Work.IMentionService>();
        services.AddOperationScoped<Upms.Application.Work.IAttachmentService>();
        services.AddOperationScoped<Upms.Application.Work.ISearchService>();
        services.AddOperationScoped<Upms.Application.Work.ISavedFilterService>();
        services.AddOperationScoped<Upms.Application.Work.ILabelService>();
        services.AddOperationScoped<Upms.Application.Notifications.INotificationService>();
        return services;
    }

    /// <summary>Shows the not-found page for failed page requests. Only GET and HEAD are re-executed: a
    /// re-executed POST would reach a Razor page without a form handler and turn every error into 400.</summary>
    public static IApplicationBuilder UseUpmsStatusCodePages(this IApplicationBuilder app)
    {
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        return app.Use((context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                && context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } feature)
            {
                feature.Enabled = false;
            }

            return next(context);
        });
    }

    public static IApplicationBuilder UseUpmsSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();

    public static IApplicationBuilder UseBlazorOriginCheck(this IApplicationBuilder app) =>
        app.UseMiddleware<BlazorOriginCheckMiddleware>();

    public static IApplicationBuilder UseMustChangePassword(this IApplicationBuilder app) =>
        app.UseMiddleware<MustChangePasswordMiddleware>();
}
