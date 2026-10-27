using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Sprints;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Sprints;

/// <summary>The report of a completed sprint (Phase 3 FR-029).</summary>
public sealed class SprintReportPageTests : BunitTestBase
{
    private static readonly DateTimeOffset Completed = new(2026, 10, 5, 16, 0, 0, TimeSpan.Zero);
    private readonly FakeSprintService _sprints = new();

    public SprintReportPageTests()
    {
        Services.AddSingleton<ISprintService>(_sprints);
    }

    private static ReportGroup Group(params ReportLine[] lines) => new(lines, lines.Sum(l => l.Estimate ?? 0));

    private static ReportLine Line(string key, decimal? estimate, bool deleted = false) =>
        new(key, WorkItemType.Story, $"Story {key}", estimate, deleted);

    private IRenderedComponent<SprintReportPage> RenderReport(int number = 1) =>
        Render<SprintReportPage>(p => p.Add(x => x.Key, "MOB").Add(x => x.Number, number));

    private void GivenReport() => _sprints.Reports[1] = new SprintReport("MOB", "Mobile App", "MOB Sprint 1", "Ship card refunds",
        new DateOnly(2026, 9, 21), new DateOnly(2026, 10, 5), Completed,
        Committed: Group(Line("MOB-2", 3), Line("MOB-3", 5), Line("MOB-6", 2)),
        Added: Group(Line("MOB-7", 8)),
        Removed: Group(Line("MOB-6", 2, deleted: true)),
        Completed: Group(Line("MOB-2", 3), Line("MOB-7", 8)),
        NotCompleted: Group(Line("MOB-3", 5)),
        CanManage: false, CanRestoreDeleted: false);

    [Fact]
    public void P3_US2_AS8_The_report_shows_five_groups_each_with_its_items_and_estimate_total()
    {
        GivenReport();

        var cut = RenderReport();

        Assert.Equal("MOB Sprint 1", cut.Find("[data-testid=report-title]").TextContent.Trim());
        Assert.Equal("Ship card refunds", cut.Find("[data-testid=report-goal]").TextContent.Trim());
        Assert.Contains("21 Sep – 5 Oct 2026", cut.Find("[data-testid=report-dates]").TextContent, StringComparison.Ordinal);
        var tables = cut.FindAll("table.report-table");
        Assert.Equal(["Committed", "Added", "Removed", "Completed", "Not completed"],
            tables.Select(t => t.QuerySelector("caption [data-testid=group-name]")!.TextContent.Trim()));
        Assert.Equal(["3 items · 10 points", "1 item · 8 points", "1 item · 2 points", "2 items · 11 points", "1 item · 5 points"],
            tables.Select(t => t.QuerySelector("[data-testid=group-total]")!.TextContent.Trim()));
        Assert.Equal(["MOB-2", "MOB-3", "MOB-6"], tables[0].QuerySelectorAll("tbody [data-testid=report-key]").Select(k => k.TextContent.Trim()));
        Assert.Contains("Deleted", tables[2].QuerySelector("tbody tr")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("spaces/MOB/backlog", cut.Find("[data-testid=back-to-backlog]").GetAttribute("href"));
    }

    [Fact]
    public void An_empty_group_says_so()
    {
        GivenReport();
        _sprints.Reports[1] = _sprints.Reports[1] with { Added = new ReportGroup([], 0) };

        var cut = RenderReport();

        Assert.Contains("None", cut.FindAll("table.report-table")[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sprint_without_a_report_shows_not_found()
    {
        var cut = RenderReport(9);

        Assert.Contains("Not found", cut.Find("h1").TextContent, StringComparison.Ordinal);
    }
}
