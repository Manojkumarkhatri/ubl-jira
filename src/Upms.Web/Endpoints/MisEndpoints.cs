using System.Globalization;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work;

namespace Upms.Web.Endpoints;

/// <summary>The MIS download (owner's decision of 2026-10-20): <c>GET /mis/{space|portfolio|department|all}/{xlsx|print}</c>,
/// with <c>key</c> (a space) or <c>id</c> (a portfolio or department) and the viewer's <c>today</c>. Who may download which
/// level is checked by the application; a refusal is an empty 404 or 403.</summary>
internal static class MisEndpoints
{
    public static IEndpointRouteBuilder MapMisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mis/{level}/{format}", ServeAsync).AllowAnonymous();
        return endpoints;
    }

    /// <summary>The address of an MIS file, for links.</summary>
    public static string Url(MisLevel level, string format, string? key, long? id, DateOnly today)
    {
        var url = $"mis/{level.ToString().ToLowerInvariant()}/{format}?today={today:yyyy-MM-dd}";
        if (key is not null)
        {
            url += $"&key={Uri.EscapeDataString(key)}";
        }

        if (id is not null)
        {
            url += $"&id={id.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        return url;
    }

    private static async Task<IResult> ServeAsync(string level, string format, HttpContext context, IMisService mis, TimeProvider time)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse<MisLevel>(level, ignoreCase: true, out var misLevel) || format is not ("xlsx" or "print"))
        {
            return Results.NotFound();
        }

        var query = context.Request.Query;
        var utcToday = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        // The viewer's own date, when it is within a day of the server's (time zones); otherwise the server's.
        var today = DateOnly.TryParseExact(query["today"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var viewer)
            && Math.Abs(viewer.DayNumber - utcToday.DayNumber) <= 1 ? viewer : utcToday;
        long? id = long.TryParse(query["id"], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var key = query["key"].ToString();
        var result = await mis.BuildAsync(misLevel, key.Length == 0 ? null : key, id, today, context.RequestAborted);
        if (!result.IsSuccess)
        {
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return result.Error!.Kind == Application.Common.Results.ErrorKind.Forbidden ? Results.StatusCode(403) : Results.NotFound();
        }

        var report = result.Value!;
        context.Response.Headers.CacheControl = "private, no-store";
        return format == "xlsx"
            ? Results.File(MisExport.Xlsx(report), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{MisExport.FileName(report)}.xlsx")
            : Results.Content(MisExport.PrintPage(report, $"{context.Request.PathBase}/"), "text/html; charset=utf-8");
    }
}
