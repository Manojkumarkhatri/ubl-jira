using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Upms.Application.Identity;
using Upms.Domain.Identity;
using Upms.Web.Security;

namespace Upms.Web.Endpoints;

/// <summary><c>POST /Account/Logout</c> (anti-forgery protected form post).</summary>
internal static class AccountEndpoints
{
    public static IEndpointConventionBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/Account/Logout", async Task<Results<RedirectHttpResult, BadRequest>> (
            HttpContext context,
            [FromServices] SignInManager<User> signInManager,
            [FromServices] SessionActivityRegistry registry,
            [FromServices] IAntiforgery antiforgery) =>
        {
            // A session that has already ended (the idle time-out, a computer that slept, a sign-out in another tab) has
            // nothing to sign out of. The form's token was made for the signed-in person and no longer matches, which used
            // to end in an error page: go to the sign-in page instead. Someone signed in still needs a valid token.
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return TypedResults.LocalRedirect("~/Account/Login");
            }

            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return TypedResults.BadRequest();
            }

            var returnUrl = (await context.Request.ReadFormAsync()).TryGetValue("returnUrl", out var value) ? value.ToString() : null;
            if (context.User.FindFirst(UpmsClaimTypes.SessionId)?.Value is { } sessionId)
            {
                registry.Expire(sessionId);
            }

            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(IsSafeRelative(returnUrl) ? $"~/{returnUrl}" : "~/Account/Login");
        }).AllowAnonymous().DisableAntiforgery();

    private static bool IsSafeRelative(string? url) =>
        !string.IsNullOrEmpty(url)
        && !url.StartsWith('/') && !url.StartsWith('\\')
        && !url.Contains("://", StringComparison.Ordinal)
        && Uri.IsWellFormedUriString(url, UriKind.Relative);
}
