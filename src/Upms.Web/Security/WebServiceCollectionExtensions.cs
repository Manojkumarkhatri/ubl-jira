using System.Security.Claims;
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
        services.AddScoped<ViewerTimeZone>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "upms.auth";
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
        });
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(1);
            options.OnRefreshingPrincipal = context =>
            {
                // Keep the session ID across refreshes so idle tracking survives stamp validation.
                if (context.CurrentPrincipal?.FindFirst(UpmsClaimTypes.SessionId) is { } sessionId
                    && context.NewPrincipal?.Identity is ClaimsIdentity identity)
                {
                    if (identity.FindFirst(UpmsClaimTypes.SessionId) is { } generated)
                    {
                        identity.RemoveClaim(generated);
                    }

                    identity.AddClaim(new Claim(UpmsClaimTypes.SessionId, sessionId.Value));
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
        services.AddOperationScoped<IOrganizationSettingsReader>();
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
