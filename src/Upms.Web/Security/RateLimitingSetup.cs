using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Upms.Web.Security;

/// <summary>Requests allowed per minute (contracts/http-endpoints.md), from the "RateLimiting" configuration
/// section. The defaults are the contract's values; only test hosts raise them.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Sign-in attempts per client IP address.</summary>
    public int SignInPerMinute { get; set; } = 10;

    /// <summary>First-run setup attempts per client IP address.</summary>
    public int SetupPerMinute { get; set; } = 5;

    /// <summary>Keep-alive calls per user.</summary>
    public int KeepAlivePerMinute { get; set; } = 6;
}

/// <summary>Rate limits (contracts/http-endpoints.md): sign-in 10 per minute per client IP, setup 5 per
/// minute per IP, keep-alive 6 per minute per user.</summary>
internal static class RateLimitingSetup
{
    public static IServiceCollection AddUpmsRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName)
            .Validate(o => o.SignInPerMinute > 0 && o.SetupPerMinute > 0 && o.KeepAlivePerMinute > 0,
                "Rate limits must be positive numbers of requests per minute.")
            .ValidateOnStart();
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(Partition);
        });
    }

    private static RateLimitPartition<string> Partition(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("none");
        }

        var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var path = context.Request.Path;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"login:{ip}", limits.SignInPerMinute);
        }

        if (path.Equals("/setup", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"setup:{ip}", limits.SetupPerMinute);
        }

        if (path.Equals("/account/keepalive", StringComparison.OrdinalIgnoreCase))
        {
            return PerMinute($"keepalive:{CurrentUser.ParseUserId(context.User)?.ToString() ?? ip}", limits.KeepAlivePerMinute);
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
