using System.Globalization;
using System.Security;
using System.Text;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Endpoints;

namespace Upms.Web.Components.Pages.Timeline;

/// <summary>What the export shows: the timeline page's own scale, colours and filters.</summary>
public sealed record TimelineExportOptions(
    TimelineZoom Zoom,
    bool ColorByEpic,
    string? Search,
    IReadOnlySet<string> People,
    IReadOnlySet<StatusCategory> Categories,
    IReadOnlySet<WorkItemType> Kinds,
    DateOnly Today)
{
    /// <summary>The page's "no one" choice in its assignee filter.</summary>
    public const string NoOne = "none";

    public bool Matches(TimelineItem item) =>
        (string.IsNullOrWhiteSpace(Search) || item.Title.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase)
            || item.Key.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase))
        && (People.Count == 0 || People.Contains(item.Assignee?.UserId.ToString() ?? NoOne))
        && (Categories.Count == 0 || Categories.Contains(item.Status.Category))
        && (Kinds.Count == 0 || Kinds.Contains(item.Type));

    public bool Matches(TimelineRow row) => Matches(row.Task) || row.ScheduledSubtasks.Any(Matches) || row.UnscheduledSubtasks.Any(Matches);
}

/// <summary>Jira's "Export" of the timeline: the chart as an SVG image (which the browser also turns into a PNG, and the
/// print page into a PDF), and its rows as a spreadsheet. Everything is drawn here from the timeline's data, with the
/// page's scale, colours and filters; every text is escaped.</summary>
public static class TimelineExport
{
    private const int Pad = 24;
    private const int LabelWidth = 360;
    private const int TitleHeight = 70;
    private const int ScaleHeight = 34;
    private const int RowHeight = 28;
    private const int MaxChartWidth = 3600;
    private const string Font = "Segoe UI, Arial, Helvetica, sans-serif";

    private sealed record Line(TimelineItem Item, int Indent, bool IsContainer, Progress? Progress, GateMarker? Gate, EpicColor? Color);

    /// <summary>The rows the chart draws, in the page's order: each epic or stage with its items and their sub-tasks, then
    /// the items in none, then those in none without dates. Items without dates have a row but no bar.</summary>
    private static List<Line> Lines(TimelineView view, TimelineExportOptions options)
    {
        var lines = new List<Line>();
        void AddRow(TimelineRow row, int indent, EpicColor? color)
        {
            lines.Add(new Line(row.Task, indent, false, null, null, color));
            lines.AddRange(row.ScheduledSubtasks.Select(s => new Line(s, indent + 1, false, null, null, color)));
            lines.AddRange(row.UnscheduledSubtasks.Select(s => new Line(s, indent + 1, false, null, null, color)));
        }

        foreach (var group in view.Groups.Where(g => options.Matches(g.Container) || g.Rows.Any(options.Matches) || g.Unscheduled.Any(options.Matches)))
        {
            lines.Add(new Line(group.Container, 0, true, group.Progress, group.Gate, group.Container.Color));
            foreach (var row in group.Rows.Where(options.Matches))
            {
                AddRow(row, 1, group.Container.Color);
            }

            lines.AddRange(group.Unscheduled.Where(options.Matches).Select(u => new Line(u, 1, false, null, null, group.Container.Color)));
        }

        foreach (var row in view.Rows.Where(options.Matches))
        {
            AddRow(row, 0, null);
        }

        lines.AddRange(view.Unscheduled.Items.Where(options.Matches).Select(u => new Line(u, 0, false, null, null, null)));
        return lines;
    }

    /// <summary>The chart as a standalone SVG document with its size in pixels, so it prints and converts as drawn.</summary>
    public static string Svg(TimelineView view, TimelineExportOptions options)
    {
        var lines = Lines(view, options);
        var dated = lines.Select(l => l.Item).Where(i => i.IsScheduled)
            .SelectMany(i => new[] { i.StartDate, i.DueDate }).OfType<DateOnly>()
            .Concat(lines.Select(l => l.Gate?.Date).OfType<DateOnly>())
            .Append(options.Today)
            .ToList();
        var (from, to) = Range(dated.Min(), dated.Max(), options.Zoom);
        var days = to.DayNumber - from.DayNumber + 1;
        var perDay = Math.Min(options.Zoom switch { TimelineZoom.Weeks => 18.0, TimelineZoom.Months => 5.0, _ => 2.0 }, MaxChartWidth / (double)days);
        var chartLeft = Pad + LabelWidth;
        var chartWidth = days * perDay;
        var width = (int)Math.Ceiling(chartLeft + chartWidth + Pad);
        var top = TitleHeight + ScaleHeight;
        var height = top + Math.Max(lines.Count, 1) * RowHeight + 64;
        double X(DateOnly date) => chartLeft + (date.DayNumber - from.DayNumber) * perDay;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" font-family=\"{Font}\" role=\"img\">");
        svg.Append(CultureInfo.InvariantCulture, $"<title>{E(view.ProjectName)} timeline</title>");
        svg.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto\">")
            .Append("<path d=\"M0 0L10 5L0 10z\" fill=\"context-stroke\"/></marker></defs>");
        svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/>");

        // Title and what the image shows.
        svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{Pad}\" y=\"34\" font-size=\"20\" font-weight=\"600\" fill=\"#172b4d\">{E(view.ProjectName)} · Timeline</text>");
        var shown = lines.Count;
        svg.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{Pad}\" y=\"56\" font-size=\"12\" fill=\"#5e6c84\">{E(options.Zoom.ToString())} · {shown} rows · {E(Day(from))} to {E(Day(to))} · exported {E(Day(options.Today))}</text>");

        // The scale: a band of period names and a line at each period's start, weekends shaded by week.
        svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{chartLeft}\" y=\"{TitleHeight}\" width=\"{F(chartWidth)}\" height=\"{ScaleHeight}\" fill=\"#f4f5f7\"/>");
        svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{Pad + 8}\" y=\"{TitleHeight + 22}\" font-size=\"12\" font-weight=\"600\" fill=\"#5e6c84\">Work</text>");
        if (options.Zoom == TimelineZoom.Weeks)
        {
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    svg.Append(CultureInfo.InvariantCulture,
                        $"<rect x=\"{F(X(day))}\" y=\"{top}\" width=\"{F(perDay)}\" height=\"{lines.Count * RowHeight}\" fill=\"#f7f8f9\"/>");
                }
            }
        }

        foreach (var (start, label) in Periods(from, to, options.Zoom))
        {
            var x = X(start);
            svg.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"{F(x)}\" y1=\"{TitleHeight}\" x2=\"{F(x)}\" y2=\"{top + lines.Count * RowHeight}\" stroke=\"#dfe1e6\" stroke-width=\"1\"/>");
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{F(x + 6)}\" y=\"{TitleHeight + 22}\" font-size=\"12\" font-weight=\"600\" fill=\"#44546f\">{E(label)}</text>");
        }

        // Rows: zebra stripes, the key and summary on the left, then the bar.
        var rowOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var y = top + i * RowHeight;
            rowOf.TryAdd(line.Item.Key, i);
            if (i % 2 == 1)
            {
                svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{Pad}\" y=\"{y}\" width=\"{F(width - 2 * Pad)}\" height=\"{RowHeight}\" fill=\"#091e42\" fill-opacity=\"0.03\"/>");
            }

            var textX = Pad + 8 + line.Indent * 16;
            var key = line.Item.Key;
            var room = (LabelWidth - 16 - line.Indent * 16 - key.Length * 7) / 6.6;
            svg.Append(CultureInfo.InvariantCulture,
                $"<text x=\"{textX}\" y=\"{y + 18}\" font-size=\"12\"><tspan fill=\"#5e6c84\">{E(key)}</tspan><tspan dx=\"8\" fill=\"#172b4d\"{(line.IsContainer ? " font-weight=\"600\"" : "")}>{E(Cut(line.Item.Title, (int)room))}</tspan></text>");

            if (!line.Item.IsScheduled)
            {
                svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{chartLeft + 8}\" y=\"{y + 18}\" font-size=\"11\" font-style=\"italic\" fill=\"#8590a2\">No dates</text>");
                continue;
            }

            var first = line.Item.StartDate ?? line.Item.DueDate!.Value;
            var last = line.Item.DueDate ?? first;
            var color = BarColor(line, options.ColorByEpic);
            if (line.Item.Type == WorkItemType.Milestone)
            {
                var mx = X(last) + perDay / 2;
                svg.Append(CultureInfo.InvariantCulture,
                    $"<path d=\"M{F(mx)} {y + 6}L{F(mx + 8)} {y + 14}L{F(mx)} {y + 22}L{F(mx - 8)} {y + 14}z\" fill=\"{color}\"/>");
                continue;
            }

            var bx = X(first);
            var bw = Math.Max((last.DayNumber - first.DayNumber + 1) * perDay, 4);
            var barHeight = line.IsContainer ? 12 : 16;
            var by = y + (RowHeight - barHeight) / 2;
            if (line.IsContainer)
            {
                // The bar as on the page, and its progress as a thin line under it.
                svg.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{F(bx)}\" y=\"{by - 2}\" width=\"{F(bw)}\" height=\"{barHeight}\" rx=\"3\" fill=\"{color}\"/>")
                    .Append(CultureInfo.InvariantCulture,
                        $"<rect x=\"{F(bx)}\" y=\"{by + barHeight + 1}\" width=\"{F(bw)}\" height=\"3\" rx=\"1.5\" fill=\"#dcdfe4\"/>");
                if (line.Progress is { } progress && progress.Percent > 0)
                {
                    svg.Append(CultureInfo.InvariantCulture,
                        $"<rect x=\"{F(bx)}\" y=\"{by + barHeight + 1}\" width=\"{F(bw * progress.Percent / 100.0)}\" height=\"3\" rx=\"1.5\" fill=\"#22a06b\"/>");
                }

                svg.Append(CultureInfo.InvariantCulture,
                    $"<text x=\"{F(bx + bw + 6)}\" y=\"{y + 18}\" font-size=\"11\" fill=\"#5e6c84\">{line.Progress?.Percent ?? 0}%</text>");
                if (line.Gate is { } gate)
                {
                    var gx = X(gate.Date) + perDay / 2;
                    var gateColor = gate.State == GateState.Approved ? "#22a06b" : "#e2b203";
                    svg.Append(CultureInfo.InvariantCulture,
                        $"<path d=\"M{F(gx)} {y + 4}L{F(gx + 7)} {y + 14}L{F(gx)} {y + 24}L{F(gx - 7)} {y + 14}z\" fill=\"{gateColor}\" stroke=\"#ffffff\"/>");
                }
            }
            else
            {
                svg.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{F(bx)}\" y=\"{by}\" width=\"{F(bw)}\" height=\"{barHeight}\" rx=\"4\" fill=\"{color}\"/>");
                if (line.Item.WaitsFor.Any(w => w.IsLate))
                {
                    svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{F(bx + bw + 4)}\" y=\"{y + 19}\" font-size=\"12\" fill=\"#c9372c\">!</text>");
                }
            }
        }

        // Dependencies between rows that are both drawn: from the end of what is waited for to the start of what waits.
        foreach (var link in view.Dependencies)
        {
            if (!rowOf.TryGetValue(link.WaitedForKey, out var fromRow) || !rowOf.TryGetValue(link.WaitingKey, out var toRow))
            {
                continue;
            }

            var waitedFor = lines[fromRow].Item;
            var waiting = lines[toRow].Item;
            if (!waitedFor.IsScheduled || !waiting.IsScheduled)
            {
                continue;
            }

            var x1 = X(waitedFor.DueDate ?? waitedFor.StartDate!.Value) + perDay;
            var x2 = X(waiting.StartDate ?? waiting.DueDate!.Value);
            var y1 = top + fromRow * RowHeight + RowHeight / 2;
            var y2 = top + toRow * RowHeight + RowHeight / 2;
            var stroke = link.IsLate ? "#c9372c" : "#8590a2";
            svg.Append(CultureInfo.InvariantCulture,
                $"<path d=\"M{F(x1)} {y1}H{F(x1 + 8)}V{y2}H{F(x2 - 2)}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"1.2\" marker-end=\"url(#arrow)\"/>");
        }

        // Today, over everything.
        if (options.Today >= from && options.Today <= to)
        {
            var tx = X(options.Today) + perDay / 2;
            svg.Append(CultureInfo.InvariantCulture,
                $"<line x1=\"{F(tx)}\" y1=\"{TitleHeight}\" x2=\"{F(tx)}\" y2=\"{top + lines.Count * RowHeight}\" stroke=\"#c9372c\" stroke-width=\"2\"/>");
            svg.Append(CultureInfo.InvariantCulture,
                $"<rect x=\"{F(tx - 22)}\" y=\"{TitleHeight - 18}\" width=\"44\" height=\"16\" rx=\"3\" fill=\"#c9372c\"/><text x=\"{F(tx)}\" y=\"{TitleHeight - 6}\" font-size=\"11\" font-weight=\"600\" fill=\"#ffffff\" text-anchor=\"middle\">Today</text>");
        }

        // The legend under the chart.
        var ly = top + lines.Count * RowHeight + 32;
        var legend = options.ColorByEpic
            ? new[] { ("#8270db", "Bars in their epic or stage colour") }
            : [("#8996a6", "To do"), ("#1d5fb4", "In progress"), ("#22a06b", "Done")];
        var lx = Pad + 8;
        foreach (var (fill, label) in legend)
        {
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{lx}\" y=\"{ly - 10}\" width=\"22\" height=\"10\" rx=\"3\" fill=\"{fill}\"/>")
                .Append(CultureInfo.InvariantCulture, $"<text x=\"{lx + 28}\" y=\"{ly}\" font-size=\"12\" fill=\"#44546f\">{E(label)}</text>");
            lx += 44 + (int)(label.Length * 6.2);
        }

        svg.Append(CultureInfo.InvariantCulture, $"<line x1=\"{lx}\" y1=\"{ly - 5}\" x2=\"{lx + 22}\" y2=\"{ly - 5}\" stroke=\"#c9372c\" stroke-width=\"2\"/>")
            .Append(CultureInfo.InvariantCulture, $"<text x=\"{lx + 28}\" y=\"{ly}\" font-size=\"12\" fill=\"#44546f\">Today</text>");
        lx += 80;
        svg.Append(CultureInfo.InvariantCulture, $"<path d=\"M{lx} {ly - 5}H{lx + 20}\" stroke=\"#8590a2\" stroke-width=\"1.2\" marker-end=\"url(#arrow)\"/>")
            .Append(CultureInfo.InvariantCulture, $"<text x=\"{lx + 28}\" y=\"{ly}\" font-size=\"12\" fill=\"#44546f\">Waits for (red: starts too early)</text>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>The rows as a spreadsheet, every item including those without dates, with its epic or stage.</summary>
    public static string Csv(TimelineView view, TimelineExportOptions options)
    {
        var csv = new StringBuilder();
        string[] header = ["Level", "Key", "Work type", "Summary", "Status", "Assignee", "Start date", "Due date", "Epic or stage", "Progress", "Waits for"];
        csv.AppendLine(string.Join(',', header.Select(CsvText.Cell)));
        string? containerOf = null;
        foreach (var line in Lines(view, options))
        {
            if (line.IsContainer)
            {
                containerOf = $"{line.Item.Key} {line.Item.Title}";
            }
            else if (line.Indent == 0)
            {
                containerOf = null;
            }

            var item = line.Item;
            string[] cells =
            [
                line.IsContainer ? "Epic or stage" : line.Indent > (containerOf is null ? 0 : 1) ? "Sub-task" : "Item",
                item.Key,
                WorkItemTypes.DisplayName(item.Type),
                item.Title,
                item.Status.Name,
                item.Assignee?.DisplayName ?? "",
                Day(item.StartDate),
                Day(item.DueDate),
                line.IsContainer ? "" : containerOf ?? "",
                line.Progress is { } progress ? $"{progress.Percent}%" : "",
                string.Join(" ", item.WaitsFor.Select(w => w.Key)),
            ];
            csv.AppendLine(string.Join(',', cells.Select(CsvText.Cell)));
        }
        return csv.ToString();
    }

    /// <summary>A whole number of periods around the dates, with one more on each side.</summary>
    private static (DateOnly From, DateOnly To) Range(DateOnly first, DateOnly last, TimelineZoom zoom)
    {
        switch (zoom)
        {
            case TimelineZoom.Weeks:
                var monday = first.AddDays(-(((int)first.DayOfWeek + 6) % 7) - 7);
                var sunday = last.AddDays(6 - (((int)last.DayOfWeek + 6) % 7) + 7);
                return (monday, sunday);
            case TimelineZoom.Months:
                var start = new DateOnly(first.Year, first.Month, 1).AddMonths(-1);
                var end = new DateOnly(last.Year, last.Month, 1).AddMonths(2).AddDays(-1);
                return (start, end);
            default:
                var quarter = new DateOnly(first.Year, ((first.Month - 1) / 3 * 3) + 1, 1).AddMonths(-3);
                var after = new DateOnly(last.Year, ((last.Month - 1) / 3 * 3) + 1, 1).AddMonths(6).AddDays(-1);
                return (quarter, after);
        }
    }

    private static IEnumerable<(DateOnly Start, string Label)> Periods(DateOnly from, DateOnly to, TimelineZoom zoom)
    {
        for (var start = from; start <= to;)
        {
            switch (zoom)
            {
                case TimelineZoom.Weeks:
                    yield return (start, start.ToString("d MMM", CultureInfo.InvariantCulture));
                    start = start.AddDays(7);
                    break;
                case TimelineZoom.Months:
                    yield return (start, start.ToString(start.Month == 1 ? "MMM yyyy" : "MMM", CultureInfo.InvariantCulture));
                    start = start.AddMonths(1);
                    break;
                default:
                    yield return (start, $"Q{((start.Month - 1) / 3) + 1} {start.Year}");
                    start = start.AddMonths(3);
                    break;
            }
        }
    }

    /// <summary>The page's bar colours: by status type, or by epic colour with items in no epic purple.</summary>
    private static string BarColor(Line line, bool byEpic)
    {
        if (byEpic)
        {
            return (line.Color ?? EpicColor.Purple) switch
            {
                EpicColor.Purple => "#8270db",
                EpicColor.Blue => "#1d7afc",
                EpicColor.Teal => "#2898bd",
                EpicColor.Green => "#22a06b",
                EpicColor.Yellow => "#e2b203",
                EpicColor.Orange => "#e06c00",
                EpicColor.Red => "#e2483d",
                _ => "#8590a2",
            };
        }

        return line.Item.Status.Category switch
        {
            StatusCategory.Done => "#22a06b",
            StatusCategory.InProgress => "#1d5fb4",
            _ => "#8996a6",
        };
    }

    private static string Cut(string text, int max) => max < 4 ? "" : text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string Day(DateOnly? date) => date?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "";

    private static string F(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string E(string text) => SecurityElement.Escape(text) ?? "";
}
