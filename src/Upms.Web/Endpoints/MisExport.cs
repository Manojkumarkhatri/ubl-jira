using System.Globalization;
using System.Net;
using System.Text;
using Upms.Application.Work;
using Upms.Domain.Common;

namespace Upms.Web.Endpoints;

/// <summary>The MIS as an Excel workbook (one sheet per table) and as a printable page that the browser saves as PDF.</summary>
internal static class MisExport
{
    public static string Title(MisReport report) => $"{report.LevelName} MIS · {report.Scope}";

    public static string FileName(MisReport report)
    {
        var scope = new string(report.Scope.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (scope.Contains("--", StringComparison.Ordinal))
        {
            scope = scope.Replace("--", "-", StringComparison.Ordinal);
        }

        return $"MIS-{(scope.Length == 0 ? "report" : scope)}-{report.Today:yyyy-MM-dd}";
    }

    public static byte[] Xlsx(MisReport report)
    {
        var book = new XlsxWriter();
        var t = report.Totals;
        book.AddSheet("Summary").Widths(34, 16, 60)
            .Row(new XlsxCell(Title(report), XlsxStyle.Title))
            .Row(new XlsxCell($"Generated {report.Today:d MMM yyyy} by {report.GeneratedBy}. Overdue and due dates are as of that day.", XlsxStyle.Muted))
            .Blank()
            .Header("Measure", "Value", "What it counts")
            .Row("Spaces", t.Spaces, "Spaces in this report")
            .Row("Work items", t.Items, "Stories, tasks, bugs and sub-tasks (not epics, stages or milestones)")
            .Row("To do", t.ToDo, "In a To do column")
            .Row("In progress", t.InProgress, "In an In progress column")
            .Row("Done", t.Done, "In a Done column")
            .Row("Done %", new XlsxCell(t.PercentDone / 100.0, XlsxStyle.Percent), "Done of all work items")
            .Row("Overdue", new XlsxCell(t.Overdue, t.Overdue > 0 ? XlsxStyle.Bad : XlsxStyle.Normal), "Open and past the due date")
            .Row($"Due in {MisRules.DueSoonDays} days", t.DueSoon, "Open and due today or in the next two weeks")
            .Row($"Milestones in {MisRules.MilestoneWindowDays} days", t.Milestones, "Open milestones due by then, late ones included")
            .Row("On track", new XlsxCell(t.OnTrack, XlsxStyle.Good), "Spaces with nothing late")
            .Row("At risk", new XlsxCell(t.AtRisk, XlsxStyle.Warning), "Spaces with late work, or a milestone due this week")
            .Row("Off track", new XlsxCell(t.OffTrack, XlsxStyle.Bad), "A fifth or more of open work late, or a milestone late");

        var spaces = book.AddSheet("Spaces").Widths(18, 20, 10, 30, 12, 26, 9, 9, 11, 9, 9, 9, 12, 28, 14, 12, 14)
            .Header("Department", "Portfolio", "Key", "Space", "Template", "Leads", "Items", "To do", "In progress", "Done", "Done %",
                "Overdue", "Due in 14 days", "Next milestone", "Milestone date", "Health", "Last updated");
        foreach (var s in report.Spaces)
        {
            spaces.Row(s.Department, s.Portfolio, s.Key, s.Name, TemplateName(s.Template), s.Leads, s.Items, s.ToDo, s.InProgress, s.Done,
                new XlsxCell(s.PercentDone / 100.0, XlsxStyle.Percent), new XlsxCell(s.Overdue, s.Overdue > 0 ? XlsxStyle.Bad : XlsxStyle.Normal),
                s.DueSoon, s.NextMilestone, s.NextMilestoneDate, new XlsxCell(s.Health.Name(), HealthStyle(s.Health)), s.LastUpdated);
        }

        var containers = book.AddSheet("Epics and stages").Widths(10, 12, 40, 9, 16, 22, 14, 14, 9, 9, 9, 8)
            .Header("Space", "Key", "Title", "Type", "Status", "Owner", "Start", "Due", "Items", "Done", "Done %", "Late");
        foreach (var c in report.Containers)
        {
            containers.Row(c.Space, c.Key, c.Title, c.Type, c.Status, c.Owner, c.Start, c.Due, c.Items, c.Done,
                new XlsxCell(c.PercentDone / 100.0, XlsxStyle.Percent), c.Late ? new XlsxCell("Yes", XlsxStyle.Bad) : "No");
        }

        var overdue = book.AddSheet("Overdue").Widths(10, 12, 10, 44, 16, 22, 14, 10, 10)
            .Header("Space", "Key", "Type", "Summary", "Status", "Assignee", "Due", "Days late", "Priority");
        foreach (var o in report.Overdue)
        {
            overdue.Row(o.Space, o.Key, o.Type, o.Title, o.Status, o.Assignee ?? "Unassigned", o.Due, new XlsxCell(o.Days, XlsxStyle.Bad), o.Priority);
        }

        var milestones = book.AddSheet("Milestones").Widths(10, 12, 40, 16, 22, 14, 10)
            .Header("Space", "Key", "Milestone", "Status", "Owner", "Due", "Days left");
        foreach (var m in report.Milestones)
        {
            milestones.Row(m.Space, m.Key, m.Title, m.Status, m.Assignee, m.Due,
                new XlsxCell(m.Days, m.Days < 0 ? XlsxStyle.Bad : m.Days <= 7 ? XlsxStyle.Warning : XlsxStyle.Normal));
        }

        var people = book.AddSheet("People").Widths(28, 10, 10, 18)
            .Header("Person", "Open", "Overdue", "Done in 30 days");
        foreach (var p in report.People)
        {
            people.Row(p.Name, p.Open, new XlsxCell(p.Overdue, p.Overdue > 0 ? XlsxStyle.Bad : XlsxStyle.Normal), p.DoneLast30Days);
        }

        return book.ToBytes();
    }

    /// <summary>A page laid out for A4 landscape that opens the print dialog, where "Save as PDF" makes the PDF.</summary>
    public static string PrintPage(MisReport report, string baseUrl)
    {
        var t = report.Totals;
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append(CultureInfo.InvariantCulture, $"<title>{E(Title(report))}</title>")
            .Append(CultureInfo.InvariantCulture, $"<link rel=\"stylesheet\" href=\"{baseUrl}mis-print.css\">")
            .Append(CultureInfo.InvariantCulture, $"<script src=\"{baseUrl}js/print-page.js\" defer></script></head><body>")
            .Append("<header class=\"mis-head\"><div class=\"brand\"><span class=\"mark\">U</span> U-PMS</div>")
            .Append(CultureInfo.InvariantCulture, $"<h1>{E(Title(report))}</h1>")
            .Append(CultureInfo.InvariantCulture,
                $"<p class=\"muted\">Generated {report.Today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)} by {E(report.GeneratedBy)} · {t.Spaces} space{(t.Spaces == 1 ? "" : "s")}</p></header>");

        html.Append("<section class=\"tiles\">");
        Tile(html, "Work items", t.Items.ToString(CultureInfo.InvariantCulture), $"{t.PercentDone}% done", "");
        Tile(html, "In progress", t.InProgress.ToString(CultureInfo.InvariantCulture), $"{t.ToDo} to do", "");
        Tile(html, "Overdue", t.Overdue.ToString(CultureInfo.InvariantCulture), "open and past due", t.Overdue > 0 ? "bad" : "");
        Tile(html, $"Due in {MisRules.DueSoonDays} days", t.DueSoon.ToString(CultureInfo.InvariantCulture), "open", "");
        Tile(html, "Milestones", t.Milestones.ToString(CultureInfo.InvariantCulture), $"in the next {MisRules.MilestoneWindowDays} days", "");
        html.Append("</section>");

        if (t.Spaces > 0)
        {
            html.Append("<section class=\"health\"><h2>Health</h2><div class=\"bar\" role=\"img\" aria-label=\"")
                .Append(CultureInfo.InvariantCulture, $"{t.OnTrack} on track, {t.AtRisk} at risk, {t.OffTrack} off track\">");
            foreach (var (count, css) in new[] { (t.OnTrack, "good"), (t.AtRisk, "warn"), (t.OffTrack, "bad"), (t.Spaces - t.OnTrack - t.AtRisk - t.OffTrack, "none") })
            {
                if (count > 0)
                {
                    html.Append(CultureInfo.InvariantCulture, $"<span class=\"{css}\" style=\"flex:{count}\"></span>");
                }
            }

            html.Append("</div><p class=\"legend\">")
                .Append(CultureInfo.InvariantCulture, $"<span class=\"dot good\"></span> On track {t.OnTrack} <span class=\"dot warn\"></span> At risk {t.AtRisk} <span class=\"dot bad\"></span> Off track {t.OffTrack}")
                .Append("</p></section>");
        }

        Table(html, "Spaces", ["Space", "Portfolio", "Leads", "Items", "Done %", "Overdue", "Next milestone", "Health"],
            report.Spaces.Select(s => new[]
            {
                $"<strong>{E(s.Name)}</strong> <span class=\"muted\">{E(s.Key)}</span>",
                E(s.Portfolio ?? "—"),
                E(s.Leads.Length == 0 ? "—" : s.Leads),
                Num(s.Items),
                Progress(s.PercentDone),
                s.Overdue > 0 ? $"<span class=\"bad-text\">{s.Overdue}</span>" : "0",
                s.NextMilestone is null ? "—" : $"{E(s.NextMilestone)} <span class=\"muted\">{Day(s.NextMilestoneDate)}</span>",
                $"<span class=\"pill {HealthCss(s.Health)}\">{s.Health.Name()}</span>",
            }));
        Table(html, "Epics and stages", ["Space", "Title", "Status", "Owner", "Due", "Progress"],
            report.Containers.Select(c => new[]
            {
                E(c.Space),
                $"{E(c.Title)} <span class=\"muted\">{E(c.Key)}</span>{(c.Late ? " <span class=\"pill bad\">Late</span>" : "")}",
                E(c.Status),
                E(c.Owner ?? "—"),
                Day(c.Due),
                $"{Progress(c.PercentDone)} <span class=\"muted\">{c.Done}/{c.Items}</span>",
            }));
        Table(html, "Milestones", ["Space", "Milestone", "Status", "Due", "Days left"],
            report.Milestones.Select(m => new[]
            {
                E(m.Space), $"{E(m.Title)} <span class=\"muted\">{E(m.Key)}</span>", E(m.Status), Day(m.Due),
                m.Days < 0 ? $"<span class=\"bad-text\">{-m.Days} late</span>" : Num(m.Days),
            }));
        Table(html, "Overdue work", ["Space", "Summary", "Assignee", "Due", "Days late", "Priority"],
            report.Overdue.Take(100).Select(o => new[]
            {
                E(o.Space), $"{E(o.Title)} <span class=\"muted\">{E(o.Key)}</span>", E(o.Assignee ?? "Unassigned"), Day(o.Due),
                $"<span class=\"bad-text\">{o.Days}</span>", E(o.Priority),
            }), report.Overdue.Count > 100 ? $"The first 100 of {report.Overdue.Count}; the Excel file lists them all." : null);
        Table(html, "People", ["Person", "Open", "Overdue", "Done in 30 days"],
            report.People.Take(50).Select(p => new[]
            {
                E(p.Name), Num(p.Open), p.Overdue > 0 ? $"<span class=\"bad-text\">{p.Overdue}</span>" : "0", Num(p.DoneLast30Days),
            }));
        html.Append("<footer class=\"muted\">U-PMS · ").Append(E(Title(report))).Append("</footer></body></html>");
        return html.ToString();
    }

    private static void Tile(StringBuilder html, string label, string value, string note, string css) =>
        html.Append(CultureInfo.InvariantCulture,
            $"<div class=\"tile {css}\"><div class=\"label\">{E(label)}</div><div class=\"value\">{E(value)}</div><div class=\"note\">{E(note)}</div></div>");

    private static void Table(StringBuilder html, string title, string[] headers, IEnumerable<string[]> rows, string? note = null)
    {
        var list = rows.ToList();
        html.Append(CultureInfo.InvariantCulture, $"<section><h2>{E(title)} <span class=\"count\">{list.Count}</span></h2>");
        if (list.Count == 0)
        {
            html.Append("<p class=\"muted\">None.</p></section>");
            return;
        }

        html.Append("<table><thead><tr>");
        foreach (var header in headers)
        {
            html.Append(CultureInfo.InvariantCulture, $"<th scope=\"col\">{E(header)}</th>");
        }

        html.Append("</tr></thead><tbody>");
        foreach (var row in list)
        {
            html.Append("<tr>");
            foreach (var cell in row)
            {
                html.Append("<td>").Append(cell).Append("</td>");
            }

            html.Append("</tr>");
        }

        html.Append("</tbody></table>");
        if (note is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(note)}</p>");
        }

        html.Append("</section>");
    }

    private static string Progress(int percent) =>
        $"<span class=\"progress\"><span style=\"width:{Math.Clamp(percent, 0, 100)}%\"></span></span> {percent}%";

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Day(DateOnly? date) => date?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "—";

    private static string TemplateName(ProjectTemplate template) => template switch
    {
        ProjectTemplate.StageGate => "Stage-gate",
        _ => template.ToString(),
    };

    private static XlsxStyle HealthStyle(MisHealth health) => health switch
    {
        MisHealth.OnTrack => XlsxStyle.Good,
        MisHealth.AtRisk => XlsxStyle.Warning,
        MisHealth.OffTrack => XlsxStyle.Bad,
        _ => XlsxStyle.Muted,
    };

    private static string HealthCss(MisHealth health) => health switch
    {
        MisHealth.OnTrack => "good",
        MisHealth.AtRisk => "warn",
        MisHealth.OffTrack => "bad",
        _ => "none",
    };
}
