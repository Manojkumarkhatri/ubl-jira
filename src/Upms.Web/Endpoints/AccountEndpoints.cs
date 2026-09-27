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
        endpoints.MapPost("/Account/Logout", async (
            HttpContext context,
            [FromServices] SignInManager<User> signInManager,
            [FromServices] SessionActivityRegistry registry,
            [FromForm] string? returnUrl) =>
        {
            if (context.User.FindFirst(UpmsClaimTypes.SessionId)?.Value is { } sessionId)
            {
                registry.Expire(sessionId);
            }

            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(IsSafeRelative(returnUrl) ? $"~/{returnUrl}" : "~/Account/Login");
        }).AllowAnonymous();

    private static bool IsSafeRelative(string? url) =>
        !string.IsNullOrEmpty(url)
        && !url.StartsWith('/') && !url.StartsWith('\\')
        && !url.Contains("://", StringComparison.Ordinal)
        && Uri.IsWellFormedUriString(url, UriKind.Relative);
}
