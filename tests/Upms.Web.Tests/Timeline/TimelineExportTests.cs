using System.Xml.Linq;
using Upms.Application.Common;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Timeline;

namespace Upms.Web.Tests.Timeline;

public sealed class TimelineExportTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly StatusOption Doing = new(2, "In Progress", StatusCategory.InProgress);
    private static readonly StatusOption Done = new(3, "Done", StatusCategory.Done);

    private static TimelineItem Item(string key, string title, StatusOption status, DateOnly? start, DateOnly? due, WorkItemType type = WorkItemType.Task) =>
        new(key, title, status, AssigneeRef.Of(Guid.Empty, "Amina Khan", true), start, due, status != Done, [1]) { Type = type };

    /// <summary>An epic with two tasks (one with a sub-task), a milestone, a task in no epic, an undated task in no epic and a
    /// late dependency.</summary>
    public static TimelineView View()
    {
        var epic = Item("UP-1", "Development & <UAT>", Doing, new(2026, 9, 28), new(2026, 11, 30), WorkItemType.Epic) with { Color = EpicColor.Teal };
        var design = Item("UP-2", "Design the \"MIS\" report", Done, new(2026, 9, 28), new(2026, 10, 9));
        var build = Item("UP-3", "Build the export", Doing, new(2026, 10, 12), new(2026, 11, 6)) with
        {
            WaitsFor = [new WaitingOn("UP-2", false)],
        };
        var subtask = Item("UP-4", "PNG download", ToDo, new(2026, 10, 19), new(2026, 10, 23), WorkItemType.Subtask);
        var golive = Item("UP-5", "Go-live", ToDo, null, new(2026, 12, 15), WorkItemType.Milestone);
        var loose = Item("UP-6", "Security review", ToDo, new(2026, 11, 2), new(2026, 11, 13));
        var group = new TimelineGroup(epic, new Progress(1, 2), null,
            [new TimelineRow(design, [], []), new TimelineRow(build, [subtask], [])], [Item("UP-7", "Undated task", ToDo, null, null)]);
        return new TimelineView("UP", "U-PMS", true, true, true, [new TimelineRow(golive, [], []), new TimelineRow(loose, [], [])], 2,
            new Page<TimelineItem>([Item("UP-8", "Write the user guide", ToDo, null, null)], 1, 1, 50), false, ProjectTemplate.Kanban)
        {
            Groups = [group],
            Dependencies = [new TimelineDependency("UP-2", "UP-3", false)],
        };
    }

    private static TimelineExportOptions Options(TimelineZoom zoom = TimelineZoom.Months, bool byEpic = false, string? search = null) =>
        new(zoom, byEpic, search, new HashSet<string>(), new HashSet<StatusCategory>(), new HashSet<WorkItemType>(), Today);

    [Theory]
    [InlineData(TimelineZoom.Weeks)]
    [InlineData(TimelineZoom.Months)]
    [InlineData(TimelineZoom.Quarters)]
    public void The_chart_is_a_well_formed_svg_with_every_row_and_escaped_text(TimelineZoom zoom)
    {
        var svg = TimelineExport.Svg(View(), Options(zoom));

        var document = XDocument.Parse(svg); // throws when the text is not escaped
        Assert.Equal("svg", document.Root!.Name.LocalName);
        Assert.Contains("Development &amp; &lt;UAT&gt;", svg, StringComparison.Ordinal);
        foreach (var key in new[] { "UP-1", "UP-2", "UP-3", "UP-4", "UP-5", "UP-6", "UP-7", "UP-8" })
        {
            Assert.Contains($">{key}<", svg, StringComparison.Ordinal);
        }

        Assert.Contains("Today", svg, StringComparison.Ordinal);
        Assert.Contains("No dates", svg, StringComparison.Ordinal); // UP-7
        Assert.Contains("marker-end=\"url(#arrow)\"", svg, StringComparison.Ordinal); // UP-3 waits for UP-2
    }

    [Fact]
    public void Filters_and_epic_colours_follow_the_page()
    {
        var svg = TimelineExport.Svg(View(), Options(byEpic: true, search: "security"));

        Assert.Contains(">UP-6<", svg, StringComparison.Ordinal);
        Assert.DoesNotContain(">UP-2<", svg, StringComparison.Ordinal);
        Assert.Contains("epic or stage colour", svg, StringComparison.Ordinal);    }

    [Fact]
    public void The_spreadsheet_has_a_row_per_item_with_its_epic_and_dates()
    {
        var csv = TimelineExport.Csv(View(), Options()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("\"Level\",\"Key\"", csv[0], StringComparison.Ordinal);
        Assert.Equal(9, csv.Length); // the header and eight items
        Assert.StartsWith("\"Item\",\"UP-8\"", csv[^1], StringComparison.Ordinal); // undated, in no epic: last
        Assert.Contains(csv, line => line.StartsWith("\"Sub-task\",\"UP-4\"", StringComparison.Ordinal));
        Assert.Contains(csv, line => line.Contains("\"UP-3\"", StringComparison.Ordinal) && line.Contains("\"UP-1 Development & <UAT>\"", StringComparison.Ordinal)
            && line.EndsWith("\"UP-2\"", StringComparison.Ordinal));
        Assert.Contains(csv, line => line.StartsWith("\"Epic or stage\",\"UP-1\"", StringComparison.Ordinal) && line.Contains("\"50%\"", StringComparison.Ordinal));
    }
}
