using System.Threading.RateLimiting;

namespace Upms.Web.Security;

/// <summary>Rate limits (contracts/http-endpoints.md): sign-in 10 per minute per client IP, setup 5 per
/// minute per IP, keep-alive 6 per minute per user.</summary>
internal static class RateLimitingSetup
{
    public static IServiceCollection AddUpmsRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
        });

    private static RateLimitPartition<string> Partition(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("none");
        }

        var path = context.Request.Path;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"login:{ip}", 10);
        }

        if (path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"setup:{ip}", 5);
        }

        if (path.Equals("/account/keepalive", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"keepalive:{CurrentUser.ParseUserId(context.User)?.ToString() ?? ip}", 6);
        }

        return RateLimitPartition.GetNoLimiter("none");
    }

    private static RateLimitPartition<string> PerMinute(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
}
