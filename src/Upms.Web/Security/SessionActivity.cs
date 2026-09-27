using Microsoft.AspNetCore.Components.Authorization;
using Upms.Application.Identity;

namespace Upms.Web.Security;

/// <summary>The current session's activity, looked up by its session ID claim.</summary>
internal sealed class SessionActivity(SessionActivityRegistry registry, IServiceProvider services, TimeProvider time)
    : ISessionActivity
{
    public DateTimeOffset LastActivity => SessionId is { } id ? registry.GetLastActivity(id) : time.GetUtcNow();

    public bool IsExpired => SessionId is { } id && registry.IsExpired(id);

    public void Touch()
    {
        if (SessionId is { } id)
        {
            registry.Touch(id);
        }
    }

    public void Expire()
    {
        if (SessionId is { } id)
        {
            registry.Expire(id);
        }
    }

    private string? SessionId
    {
        get
        {
            try
            {
                var state = services.GetService<AuthenticationStateProvider>()?.GetAuthenticationStateAsync();
                if (state is { IsCompletedSuccessfully: true })
                {
                    return state.Result.User.FindFirst(UpmsClaimTypes.SessionId)?.Value;
                }
            }
            catch (InvalidOperationException)
            {
                // Outside a component render; fall back to the request.
            }

            return services.GetService<IHttpContextAccessor>()?.HttpContext?.User.FindFirst(UpmsClaimTypes.SessionId)?.Value;
        }
    }
}
