using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Upms.Domain.Identity;

namespace Upms.Web.Security;

/// <summary>Development only: signs every visitor in as one account (<c>DevSignIn:UserName</c>), so the app can be tried
/// without the sign-in page until the company's LDAP sign-in is connected for UAT. The normal sign-in stays in place and
/// works as before once the setting is removed. <see cref="Guard"/> stops the app from starting with the setting outside
/// Development.</summary>
internal sealed partial class DevSignInMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<DevSignInMiddleware> logger)
{
    public const string SettingName = "DevSignIn:UserName";

    private const string LoginPath = "/Account/Login";

    private static readonly string[] SkippedPrefixes = ["/_framework", "/_content", "/_blazor", "/health", "/account/keepalive"];

    /// <summary>Whether the setting is on; it is refused outside Development, so a test or production server never signs
    /// people in without a password.</summary>
    public static bool Guard(IWebHostEnvironment environment, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[SettingName]))
        {
            return false;
        }

        return environment.IsDevelopment()
            ? true
            : throw new InvalidOperationException($"{SettingName} signs everyone in without a password and works only in Development. Remove it.");
    }

    public async Task InvokeAsync(HttpContext context, SignInManager<User> signIn)
    {
        var path = context.Request.Path;
        if (context.User.Identity?.IsAuthenticated == true || !HttpMethods.IsGet(context.Request.Method) || Path.HasExtension(path.Value)
            || Array.Exists(SkippedPrefixes, p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var userName = configuration[SettingName]!.Trim();
        var user = await signIn.UserManager.FindByNameAsync(userName);
        if (user is not { IsActive: true })
        {
            LogNoAccount(logger, userName);
            await next(context);
            return;
        }

        await signIn.SignInAsync(user, isPersistent: false);
        LogSignedIn(logger, userName);

        // The same address again, now with the cookie; from the sign-in page, where it was going.
        context.Response.Redirect(path.StartsWithSegments(LoginPath, StringComparison.OrdinalIgnoreCase)
            ? LocalOrHome(context.Request.Query["ReturnUrl"].ToString())
            : context.Request.GetEncodedPathAndQuery());
    }

    /// <summary>Only addresses on this site, never another one ("//host" or "/\host").</summary>
    private static string LocalOrHome(string url) =>
        url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal) ? url : "/";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Development sign-in: signed in as {UserName} without a password.")]
    private static partial void LogSignedIn(ILogger logger, string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Development sign-in: no active account {UserName}; the sign-in page is shown.")]
    private static partial void LogNoAccount(ILogger logger, string userName);
}
