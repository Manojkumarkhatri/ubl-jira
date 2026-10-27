using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using Upms.Application.Work;

namespace Upms.Web.Endpoints;

/// <summary><c>GET /attachments/{id}</c> and <c>/attachments/{id}/preview</c> (Phase 4 FR-027, contracts/http-endpoints.md,
/// research R13): access is checked on every request, every refusal is the same 404, anonymous requests get 401, and the
/// responses cannot run as a page (no sniffing, a sandbox, no caching).</summary>
internal static partial class AttachmentEndpoints
{
    public const string PathPrefix = "/attachments";

    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anonymous here so the cookie scheme does not redirect to sign-in: the handler answers 401 itself.
        var group = endpoints.MapGroup(PathPrefix).AllowAnonymous();
        group.MapGet("/{id:long}", (long id, HttpContext context, IAttachmentService attachments, ILogger<Program> logger) =>
            ServeAsync(id, preview: false, context, attachments, logger));
        group.MapGet("/{id:long}/preview", (long id, HttpContext context, IAttachmentService attachments, ILogger<Program> logger) =>
            ServeAsync(id, preview: true, context, attachments, logger));
        return endpoints;
    }

    private static async Task<IResult> ServeAsync(long id, bool preview, HttpContext context, IAttachmentService attachments, ILogger logger)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var opened = await attachments.OpenAsync(id, preview, context.RequestAborted);
        LogServed(logger, id, preview, opened.IsSuccess ? "served" : "not found", Activity.Current?.TraceId.ToString());
        if (!opened.IsSuccess)
        {
            // The same empty 404 for every refusal: no status page, so nothing differs between them (FR-027).
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return Results.NotFound();
        }

        var file = opened.Value!;
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        context.Items[Security.SecurityHeadersMiddleware.OwnPolicyKey] = true;
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        headers.CacheControl = "private, no-store";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        if (preview)
        {
            headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.FileName }.ToString();
            return Results.Stream(file.Content, file.ContentType);
        }

        return Results.Stream(file.Content, file.ContentType, fileDownloadName: file.FileName);
    }

    // The log holds the id and the outcome, never the file name or content.
    [LoggerMessage(Level = LogLevel.Information, Message = "Attachment {AttachmentId} (preview: {Preview}) {Outcome}; trace {TraceId}.")]
    private static partial void LogServed(ILogger logger, long attachmentId, bool preview, string outcome, string? traceId);
}
