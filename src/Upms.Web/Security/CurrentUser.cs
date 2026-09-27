using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Upms.Application.Common;

namespace Upms.Web.Security;

/// <summary>The caller for application services (tasks.md T038). In an interactive circuit the user
/// comes from the (revalidated) authentication state; in plain HTTP requests from the request; inside
/// an operation scope from the scope that started the operation. An idle-expired session has no user.</summary>
internal sealed class CurrentUser(IServiceProvider services) : ICurrentUser
{
    private bool _hasOperationUser;
    private Guid? _operationUserId;

    public Guid? UserId => _hasOperationUser ? _operationUserId : ResolveAmbientUser();

    /// <summary>Pins the caller of an operation scope (see <see cref="OperationScope"/>).</summary>
    public void UseOperationUser(Guid? userId)
    {
        _hasOperationUser = true;
        _operationUserId = userId;
    }

    public static Guid? ParseUserId(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
        && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    private Guid? ResolveAmbientUser()
    {
        if (services.GetService<ISessionActivity>() is { IsExpired: true })
        {
            return null;
        }

        return TryGetFromAuthenticationState(out var fromState)
            ? fromState
            : ParseUserId(services.GetService<IHttpContextAccessor>()?.HttpContext?.User);
    }

    private bool TryGetFromAuthenticationState(out Guid? userId)
    {
        userId = null;
        if (services.GetService<AuthenticationStateProvider>() is not { } provider)
        {
            return false;
        }

        try
        {
            var state = provider.GetAuthenticationStateAsync();
            userId = state.IsCompletedSuccessfully ? ParseUserId(state.Result.User) : null;
            return true;
        }
        catch (InvalidOperationException)
        {
            // Not a Razor component render or circuit (for example middleware or a minimal API endpoint).
            return false;
        }
    }
}
