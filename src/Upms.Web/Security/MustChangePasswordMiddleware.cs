using Upms.Application.Identity;

namespace Upms.Web.Security;

/// <summary>Sends users who still have a temporary password to the change-password page before anything
/// else (FR-003). Interactive navigation is guarded in <c>Routes.razor</c>.</summary>
internal sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public const string ChangePasswordPath = "/Account/ChangePassword";

    private static readonly string[] AllowedPrefixes =
        [ChangePasswordPath, "/Account/Logout", "/Account/Login", "/account/keepalive", "/_framework", "/_content", "/health", "/css", "/js", "/not-found", "/Error"];

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(UpmsClaimTypes.MustChangePassword, "true") && !IsAllowed(context.Request.Path))
        {
            context.Response.Redirect(ChangePasswordPath);
            return Task.CompletedTask;
        }

        return next(context);
    }

    private static bool IsAllowed(PathString path) =>
        Array.Exists(AllowedPrefixes, p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
        || Path.HasExtension(path.Value);
}
