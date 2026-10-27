namespace Upms.Web.Security;

/// <summary>Security headers on every response (contracts/http-endpoints.md, research R9). Styles allow
/// inline attributes because Blazor's virtualization writes spacer sizes as style attributes; scripts
/// come from this site only.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    /// <summary>Set in <see cref="HttpContext.Items"/> by a response that sends its own, stricter policy.</summary>
    public const string OwnPolicyKey = "Upms.OwnContentSecurityPolicy";

    private readonly string _contentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data:",
        "font-src 'self'",
        environment.IsDevelopment() ? "connect-src 'self' ws: wss: http://localhost:* https://localhost:*" : "connect-src 'self'",
        "frame-ancestors 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "object-src 'none'");

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            // A response may set a stricter policy of its own, as attachment downloads do (Phase 4 research R13).
            if (!context.Items.ContainsKey(OwnPolicyKey))
            {
                headers.ContentSecurityPolicy = _contentSecurityPolicy;
            }

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (context.Request.IsHttps)
            {
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            }

            // Pages show project data: browsers must not keep copies (ASVS 8.2.1). Static files set their own caching.
            if (!headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}
