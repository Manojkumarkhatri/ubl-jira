using System.Globalization;
using System.Text;
using Upms.Application.Common;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Timeline;

namespace Upms.Web.Endpoints;

/// <summary>Jira's "Export" of a space's timeline: <c>GET /spaces/{key}/timeline/export/{svg|csv|print}</c>, with the
/// page's scale, colours and filters in the query (<c>zoom</c>, <c>hide</c>, <c>colors</c>, <c>q</c>, <c>people</c>,
/// <c>status</c>, <c>types</c>, <c>today</c>). Access is checked as for the timeline; a refusal is an empty 404.</summary>
internal static class TimelineExportEndpoints
{
    public static IEndpointRouteBuilder MapTimelineExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/spaces/{key}/timeline/export/{format}", ServeAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> ServeAsync(string key, string format, HttpContext context, ITimelineService timeline,
        Application.Common.ICallerContext caller, TimeProvider time)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (format is not ("svg" or "csv" or "print"))
        {
            return Results.NotFound();
        }

        if (await caller.GetAsync(context.RequestAborted) is not { } me || !me.Can(Rights.Export))
        {
            // The "Export" tick (owner's decision of 2026-10-20).
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = context.Request.Query;
        var result = await timeline.GetAsync(key, string.Equals(query["hide"], "true", StringComparison.OrdinalIgnoreCase), context.RequestAborted);
        if (!result.IsSuccess)
        {
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return Results.NotFound();
        }

        var view = result.Value!;
        if (view.Unscheduled.TotalCount > view.Unscheduled.Items.Count)
        {
            // The page shows the first undated items in no epic; the export lists them all.
            var undated = new List<TimelineItem>();
            for (var page = 1; undated.Count < view.Unscheduled.TotalCount; page++)
            {
                var more = await timeline.ListUnscheduledAsync(view.ProjectKey, view.HidingCompleted, new PageRequest(page, PageRequest.MaxPageSize),
                    context.RequestAborted);
                if (!more.IsSuccess || more.Value!.Items.Count == 0)
                {
                    break;
                }

                undated.AddRange(more.Value.Items);
            }

            view = view with { Unscheduled = new Page<TimelineItem>(undated, undated.Count, 1, Math.Max(1, undated.Count)) };
        }

        var options = OptionsFrom(query, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        var name = $"{view.ProjectKey}-timeline-{options.Today:yyyy-MM-dd}";
        context.Response.Headers.CacheControl = "private, no-store";
        switch (format)
        {
            case "csv":
                return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(TimelineExport.Csv(view, options))).ToArray(),
                    "text/csv; charset=utf-8", $"{name}.csv");
            case "svg":
                // Drawn here from escaped text, and still served as a download that cannot run as a page.
                context.Items[Security.SecurityHeadersMiddleware.OwnPolicyKey] = true;
                context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
                return Results.File(Encoding.UTF8.GetBytes(TimelineExport.Svg(view, options)), "image/svg+xml", $"{name}.svg");
            default:
                var page = new StringBuilder();
                var baseUrl = $"{context.Request.PathBase}/";
                page.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
                    .Append(CultureInfo.InvariantCulture, $"<title>{System.Net.WebUtility.HtmlEncode(view.ProjectName)} timeline</title>")
                    .Append("<style>@page{size:A3 landscape;margin:10mm}body{margin:0;padding:16px;background:#fff}svg{width:100%;height:auto;display:block}")
                    .Append("@media print{body{padding:0}}</style>")
                    .Append(CultureInfo.InvariantCulture, $"<script src=\"{baseUrl}js/print-page.js\" defer></script></head><body>")
                    .Append(TimelineExport.Svg(view, options))
                    .Append("</body></html>");
                return Results.Content(page.ToString(), "text/html; charset=utf-8");
        }
    }

    private static TimelineExportOptions OptionsFrom(IQueryCollection query, DateOnly utcToday)
    {
        var zoom = Enum.TryParse<TimelineZoom>(query["zoom"], ignoreCase: true, out var z) ? z : TimelineZoom.Months;
        HashSet<string> List(string name) =>
            [.. (query[name].ToString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        var categories = List("status").Select(s => s.ToLowerInvariant() switch
        {
            "todo" => (StatusCategory?)StatusCategory.ToDo,
            "inprogress" => StatusCategory.InProgress,
            "done" => StatusCategory.Done,
            _ => null,
        }).OfType<StatusCategory>().ToHashSet();
        var kinds = List("types").Select(t => Enum.TryParse<WorkItemType>(t, ignoreCase: true, out var kind) ? kind : (WorkItemType?)null)
            .OfType<WorkItemType>().ToHashSet();

        // The viewer's own date, when it is within a day of the server's (time zones); otherwise the server's.
        var today = DateOnly.TryParseExact(query["today"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var viewer)
            && Math.Abs(viewer.DayNumber - utcToday.DayNumber) <= 1 ? viewer : utcToday;
        var search = query["q"].ToString();
        return new TimelineExportOptions(zoom, string.Equals(query["colors"], "epic", StringComparison.OrdinalIgnoreCase),
            search.Length > 200 ? search[..200] : search, List("people"), categories, kinds, today);
    }
}
