using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Upms.Application.Identity;
using Upms.Web.Security;

namespace Upms.Web.Endpoints;

/// <summary><c>POST /account/keepalive</c> (FR-006): the browser reports user activity; the session's
/// idle clock restarts and the cookie is re-issued with a fresh expiry. 204, or 401 once expired.</summary>
internal static class KeepAliveEndpoint
{
    public const string Path = "/account/keepalive";
    public const string RequestHeader = "X-Upms-Request";

    public static IEndpointConventionBuilder MapKeepAlive(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Path, HandleAsync).AllowAnonymous();

    private static async Task<IResult> HandleAsync(HttpContext context, SessionActivityRegistry registry)
    {
        // A custom header cannot be sent cross-site without a CORS preflight, which this app never allows.
        if (context.Request.Headers[RequestHeader] != "keepalive")
        {
            return Results.BadRequest();
        }

        var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var sessionId = authentication.Principal?.FindFirst(UpmsClaimTypes.SessionId)?.Value;
        if (!authentication.Succeeded || authentication.Principal is null || sessionId is null || registry.IsExpired(sessionId))
        {
            return Results.Unauthorized();
        }

        var properties = authentication.Properties ?? new AuthenticationProperties();
        properties.IssuedUtc = null;
        properties.ExpiresUtc = null;
        await context.SignInAsync(IdentityConstants.ApplicationScheme, authentication.Principal, properties);
        registry.Touch(sessionId);
        return Results.NoContent();
    }
}
