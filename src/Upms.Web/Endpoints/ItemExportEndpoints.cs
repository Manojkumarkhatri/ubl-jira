using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Endpoints;

/// <summary>Jira's "Print", "Export Excel", "Export Word" and "Export XML" for one work item:
/// <c>GET /work-items/{key}/print</c> and <c>/work-items/{key}/export/{csv|doc|xml}</c>. Access is checked as for the
/// drawer, and a refusal is the same 404 as a key that does not exist.</summary>
internal static class ItemExportEndpoints
{
    public const string PathPrefix = "/work-items";

    public static IEndpointRouteBuilder MapItemExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(PathPrefix).AllowAnonymous();
        group.MapGet("/{key}/print", (string key, HttpContext context, IWorkItemService items, IWorkItemActionService actions,
            Application.Common.ICallerContext caller) => ServeAsync(key, "print", context, items, actions, caller));
        group.MapGet("/{key}/export/{format}", (string key, string format, HttpContext context, IWorkItemService items,
            IWorkItemActionService actions, Application.Common.ICallerContext caller) => ServeAsync(key, format, context, items, actions, caller));
        return endpoints;
    }

    private static async Task<IResult> ServeAsync(string key, string format, HttpContext context, IWorkItemService items,
        IWorkItemActionService actions, Application.Common.ICallerContext caller)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        if (format is not ("print" or "csv" or "doc" or "xml"))
        {
            return Results.NotFound();
        }

        if (format != "print" && (await caller.GetAsync(context.RequestAborted) is not { } me || !me.Can(Domain.Common.Rights.Export)))
        {
            // The "Export" tick (owner's decision of 2026-10-20); printing needs none.
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var details = await items.GetAsync(key, context.RequestAborted);
        var time = details.IsSuccess ? await actions.GetTimeAsync(details.Value!.Key, context.RequestAborted) : default;
        if (!details.IsSuccess || !time.IsSuccess)
        {
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>()?.Enabled = false;
            return Results.NotFound();
        }

        var item = new ItemExport(details.Value!, time.Value!, $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/");
        context.Response.Headers.CacheControl = "private, no-store";
        var name = item.Details.Key;
        return format switch
        {
            "csv" => Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(item.Csv())).ToArray(), "text/csv; charset=utf-8", $"{name}.csv"),
            "doc" => Results.File(Encoding.UTF8.GetBytes(item.Html(forWord: true)), "application/msword", $"{name}.doc"),
            "xml" => Results.File(Encoding.UTF8.GetBytes(item.Xml()), "application/xml; charset=utf-8", $"{name}.xml"),
            _ => Results.Content(item.Html(forWord: false), "text/html; charset=utf-8"),
        };
    }

    /// <summary>One item's fields, in the order Jira's exports list them.</summary>
    private sealed record ItemExport(WorkItemDetails Details, TimeTracking Time, string BaseUrl)
    {
        private string Link => $"{BaseUrl}spaces/{Details.ProjectKey}/board?task={Details.Key}";

        private IEnumerable<(string Name, string Value)> Fields()
        {
            var d = Details;
            yield return ("Key", d.Key);
            yield return ("Summary", d.Title);
            yield return ("Space", d.ProjectName);
            yield return ("Work type", WorkItemTypes.DisplayName(d.Type));
            yield return ("Status", d.Status.Name);
            yield return ("Priority", d.Priority.ToString());
            yield return ("Assignee", d.Assignee?.DisplayName ?? "Unassigned");
            yield return ("Reporter", d.CreatorName);
            yield return ("Created", Stamp(d.CreatedAt));
            yield return ("Updated", Stamp(d.UpdatedAt));
            yield return ("Resolved", d.ResolvedAt is { } resolved ? Stamp(resolved) : "");
            yield return ("Start date", Day(d.StartDate));
            yield return ("Due date", Day(d.DueDate));
            yield return ("Labels", string.Join(", ", d.Labels));
            yield return ("Parent", d.Parent is { } parent ? $"{parent.Key} {parent.Title}" : "");
            yield return (d.Template == Domain.Common.ProjectTemplate.StageGate ? "Stage" : "Epic",
                d.Container is { } container ? $"{container.Key} {container.Title}" : "");
            yield return ("Sprint", d.Sprint?.Name ?? "");
            yield return ("Story points", WorkItem.FormatEstimate(d.Estimate) ?? "");
            yield return ("Original estimate", WorkDuration.FormatOrNull(Time.OriginalMinutes) ?? "");
            yield return ("Remaining estimate", WorkDuration.FormatOrNull(Time.RemainingMinutes) ?? "");
            yield return ("Time spent", Time.SpentMinutes > 0 ? WorkDuration.Format(Time.SpentMinutes) : "");
            yield return ("Votes", d.Votes.ToString(CultureInfo.InvariantCulture));
            yield return ("Watchers", (d.Watchers?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
            yield return ("Sub-tasks", string.Join(", ", d.Subtasks.Items.Select(s => s.Key)));
            yield return ("Description", d.Description?.PlainText ?? "");
        }

        private IEnumerable<CommentView> Comments => Details.Comments.Items.Where(c => !c.IsDeleted && c.Body is not null);

        /// <summary>Jira's "Export Excel CSV": a header row and one row, one "Comment" column per comment.</summary>
        public string Csv()
        {
            var fields = Fields().ToList();
            var comments = Comments.Select(c => $"{Stamp(c.CreatedAt)};{c.AuthorName};{c.Body!.PlainText}").ToList();
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', fields.Select(f => CsvText.Cell(f.Name)).Concat(comments.Select(_ => "Comment"))));
            csv.AppendLine(string.Join(',', fields.Select(f => CsvText.Cell(f.Value)).Concat(comments.Select(CsvText.Cell))));
            return csv.ToString();
        }

        /// <summary>The printable page, or the same page as a Word document (Word opens HTML saved as .doc).</summary>
        public string Html(bool forWord)
        {
            var d = Details;
            var html = new StringBuilder();
            html.Append(forWord
                ? "<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w=\"urn:schemas-microsoft-com:office:word\"><head><meta charset=\"utf-8\">"
                : "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            html.Append(CultureInfo.InvariantCulture, $"<title>[{E(d.Key)}] {E(d.Title)}</title>");
            html.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;color:#172b4d;margin:24px;font-size:14px}h1{font-size:20px;margin:0 0 4px}")
                .Append(".key{color:#5e6c84;font-size:13px}table{border-collapse:collapse;margin:16px 0;width:100%}th,td{border:1px solid #dfe1e6;")
                .Append("padding:6px 8px;text-align:left;vertical-align:top}th{background:#f4f5f7;width:180px;font-weight:600}h2{font-size:16px;margin:20px 0 8px}")
                .Append(".text{white-space:pre-wrap}.comment{border-top:1px solid #dfe1e6;padding:8px 0}.meta{color:#5e6c84;font-size:12px}")
                .Append("@media print{body{margin:0}}</style>");
            if (!forWord)
            {
                html.Append(CultureInfo.InvariantCulture, $"<script src=\"{E(BaseUrl)}js/print-page.js\" defer></script>");
            }

            html.Append(CultureInfo.InvariantCulture, $"</head><body><p class=\"key\">{E(d.ProjectName)} / {E(d.Key)}</p><h1>{E(d.Title)}</h1>");
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"meta\">{E(Link)}</p><table>");
            foreach (var (name, value) in Fields().Where(f => f.Name is not ("Key" or "Summary" or "Description" or "Space")))
            {
                html.Append(CultureInfo.InvariantCulture, $"<tr><th scope=\"row\">{E(name)}</th><td>{(value.Length == 0 ? "None" : E(value))}</td></tr>");
            }

            html.Append("</table><h2>Description</h2>");
            html.Append(CultureInfo.InvariantCulture, $"<div class=\"text\">{(d.Description is { } text ? E(text.PlainText) : "None")}</div>");
            if (d.Subtasks.Items.Count > 0)
            {
                html.Append("<h2>Sub-tasks</h2><table><tr><th scope=\"col\">Key</th><th scope=\"col\">Summary</th><th scope=\"col\">Status</th></tr>");
                foreach (var subtask in d.Subtasks.Items)
                {
                    html.Append(CultureInfo.InvariantCulture, $"<tr><td>{E(subtask.Key)}</td><td>{E(subtask.Title)}</td><td>{E(subtask.Status.Name)}</td></tr>");
                }

                html.Append("</table>");
            }

            if (Time.Logs.Count > 0)
            {
                html.Append("<h2>Work log</h2>");
                foreach (var log in Time.Logs)
                {
                    html.Append(CultureInfo.InvariantCulture,
                        $"<div class=\"comment\"><div class=\"meta\">{E(log.AuthorName)} logged {E(WorkDuration.Format(log.Minutes))} · {E(Stamp(log.StartedAt))}</div>");
                    html.Append(CultureInfo.InvariantCulture, $"<div class=\"text\">{E(log.Note ?? "")}</div></div>");
                }
            }

            html.Append("<h2>Comments</h2>");
            var any = false;
            foreach (var comment in Comments)
            {
                any = true;
                html.Append(CultureInfo.InvariantCulture,
                    $"<div class=\"comment\"><div class=\"meta\">{E(comment.AuthorName)} · {E(Stamp(comment.CreatedAt))}</div><div class=\"text\">{E(comment.Body!.PlainText)}</div></div>");
            }

            html.Append(any ? "" : "<p>None</p>");
            html.Append("</body></html>");
            return html.ToString();
        }

        /// <summary>Jira's XML export: an RSS channel with one item.</summary>
        public string Xml()
        {
            var d = Details;
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using var stream = new MemoryStream();
            using (var xml = XmlWriter.Create(stream, settings))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("rss");
                xml.WriteAttributeString("version", "0.92");
                xml.WriteStartElement("channel");
                xml.WriteElementString("title", "U-PMS");
                xml.WriteElementString("link", BaseUrl);
                xml.WriteStartElement("item");
                xml.WriteElementString("title", $"[{d.Key}] {d.Title}");
                xml.WriteElementString("link", Link);
                xml.WriteStartElement("space");
                xml.WriteAttributeString("key", d.ProjectKey);
                xml.WriteString(d.ProjectName);
                xml.WriteEndElement();
                xml.WriteElementString("description", d.Description?.PlainText ?? "");
                xml.WriteElementString("key", d.Key);
                xml.WriteElementString("summary", d.Title);
                xml.WriteElementString("type", WorkItemTypes.DisplayName(d.Type));
                xml.WriteElementString("priority", d.Priority.ToString());
                xml.WriteElementString("status", d.Status.Name);
                xml.WriteElementString("statusCategory", d.Status.Category.ToString());
                xml.WriteElementString("assignee", d.Assignee?.DisplayName ?? "Unassigned");
                xml.WriteElementString("reporter", d.CreatorName);
                xml.WriteStartElement("labels");
                foreach (var label in d.Labels)
                {
                    xml.WriteElementString("label", label);
                }

                xml.WriteEndElement();
                xml.WriteElementString("created", Stamp(d.CreatedAt));
                xml.WriteElementString("updated", Stamp(d.UpdatedAt));
                xml.WriteElementString("resolved", d.ResolvedAt is { } resolved ? Stamp(resolved) : "");
                xml.WriteElementString("start", Day(d.StartDate));
                xml.WriteElementString("due", Day(d.DueDate));
                xml.WriteElementString("votes", d.Votes.ToString(CultureInfo.InvariantCulture));
                xml.WriteElementString("watches", (d.Watchers?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
                Seconds(xml, "timeoriginalestimate", Time.OriginalMinutes);
                Seconds(xml, "timeestimate", Time.RemainingMinutes);
                Seconds(xml, "timespent", Time.SpentMinutes > 0 ? Time.SpentMinutes : null);
                if (d.Parent is { } parent)
                {
                    xml.WriteElementString("parent", parent.Key);
                }

                xml.WriteStartElement("subtasks");
                foreach (var subtask in d.Subtasks.Items)
                {
                    xml.WriteElementString("subtask", subtask.Key);
                }

                xml.WriteEndElement();
                xml.WriteStartElement("comments");
                foreach (var comment in Comments)
                {
                    xml.WriteStartElement("comment");
                    xml.WriteAttributeString("author", comment.AuthorName);
                    xml.WriteAttributeString("created", Stamp(comment.CreatedAt));
                    xml.WriteString(comment.Body!.PlainText);
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteStartElement("worklogs");
                foreach (var log in Time.Logs)
                {
                    xml.WriteStartElement("worklog");
                    xml.WriteAttributeString("author", log.AuthorName);
                    xml.WriteAttributeString("started", Stamp(log.StartedAt));
                    xml.WriteAttributeString("seconds", (log.Minutes * 60).ToString(CultureInfo.InvariantCulture));
                    xml.WriteString(log.Note ?? "");
                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndDocument();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void Seconds(XmlWriter xml, string name, int? minutes)
        {
            if (minutes is not { } value)
            {
                return;
            }

            xml.WriteStartElement(name);
            xml.WriteAttributeString("seconds", (value * 60).ToString(CultureInfo.InvariantCulture));
            xml.WriteString(WorkDuration.Format(value));
            xml.WriteEndElement();
        }

        private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        private static string Day(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

        private static string E(string text) => WebUtility.HtmlEncode(text);

    }
}
