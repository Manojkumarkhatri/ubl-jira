using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Upms.Web.Endpoints;

/// <summary><c>/health/live</c> (process) and <c>/health/ready</c> (database), with no internal details.</summary>
internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
            .AllowAnonymous();
    }
}
