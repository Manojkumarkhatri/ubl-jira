using Microsoft.AspNetCore.Authorization;
using Upms.Application.Common;

namespace Upms.Web.Security;

/// <summary>Authorization policies (research R7). Rights are read from the database, never from the
/// cookie, so a deactivation applies on the next request.</summary>
public static class AuthorizationPolicies
{
    public const string Administrator = "Administrator";

    public static void Configure(AuthorizationBuilder builder)
    {
        // Every page and endpoint requires a signed-in, active user unless marked [AllowAnonymous].
        builder.SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement())
            .Build());
        builder.AddPolicy(Administrator, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement(), new AdministratorRequirement()));
    }
}

public sealed class ActiveUserRequirement : IAuthorizationRequirement;

public sealed class AdministratorRequirement : IAuthorizationRequirement;

internal sealed class UserStatusAuthorizationHandler(IServiceScopeFactory scopes) : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.ToList();
        if (!pending.Exists(r => r is ActiveUserRequirement or AdministratorRequirement)
            || CurrentUser.ParseUserId(context.User) is not { } userId)
        {
            return;
        }

        // A scope of its own: in a Blazor circuit, checks from several components can overlap, and the circuit's
        // shared DbContext allows only one query at a time.
        await using var scope = scopes.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<IUserStatusReader>().GetAsync(userId, CancellationToken.None);
        foreach (var requirement in pending)
        {
            var satisfied = requirement switch
            {
                ActiveUserRequirement => status is { IsActive: true },
                AdministratorRequirement => status is { IsActive: true, IsAdministrator: true },
                _ => false,
            };
            if (satisfied)
            {
                context.Succeed(requirement);
            }
        }
    }
}
