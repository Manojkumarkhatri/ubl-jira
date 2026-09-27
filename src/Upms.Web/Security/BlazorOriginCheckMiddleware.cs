namespace Upms.Web.Security;

/// <summary>Rejects cross-origin requests to the Blazor hub (cross-site WebSocket hijacking, research R9).
/// The allowed origin is <c>App:PublicBaseUrl</c>, or the request's own origin when it is not set.</summary>
internal sealed class BlazorOriginCheckMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? _publicOrigin = ToOrigin(configuration["App:PublicBaseUrl"]);

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            && context.Request.Headers.Origin is { Count: > 0 } origins)
        {
            var allowed = _publicOrigin ?? $"{context.Request.Scheme}://{context.Request.Host}";
            if (!string.Equals(ToOrigin(origins[0]), allowed, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        }

        return next(context);
    }

    private static string? ToOrigin(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
}
