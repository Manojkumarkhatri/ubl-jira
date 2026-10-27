using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Summary;

namespace Upms.Web.Tests.Summary;

/// <summary>The Summary page (Jira parity pass US4 scenario 7, FR-022). The test clock's today is 27 Sep 2026.</summary>
public sealed class SummaryPageTests : BunitTestBase
{
    private readonly FakeSummaries _summaries = new();

    public SummaryPageTests() => Services.AddSingleton<ISpaceSummaryService>(_summaries);

    private IRenderedComponent<SummaryPage> Show() => Render<SummaryPage>(p => p.Add(x => x.Key, "WEB"));

    [Fact]
    public void JP_US4_AS7_Every_section_has_its_heading_and_each_count_opens_the_search_with_those_items()
    {
        var cut = Show();

        Assert.Equal(["Status overview", "Recent activity", "Priority breakdown", "Types of work", "Team workload", "Epic progress"],
            cut.FindAll(".sum-card h2").Select(h => h.TextContent.Trim()));
        var counts = cut.FindAll("[data-testid=summary-counts] a");
        Assert.Equal("2 completed in the last 7 days", counts[0].TextContent.Trim());
        Assert.Equal("search?space=WEB&resolved=2026-09-21..2026-09-27", counts[0].GetAttribute("href"));
        Assert.Equal("search?space=WEB&created=2026-09-21..2026-09-27", counts[2].GetAttribute("href"));
        Assert.Equal("search?space=WEB&category=todo,inprogress&due=..2026-10-04", Uri.UnescapeDataString(counts[3].GetAttribute("href")!));
        Assert.Equal("search?space=WEB&kind=bug", cut.Find("[data-testid=types-of-work] tbody a").GetAttribute("href"));
    }

    [Fact]
    public void Activity_reads_as_sentences_and_epics_show_their_progress()
    {
        var cut = Show();

        // As in Jira: the person's avatar (hidden from screen readers, who hear the name), the sentence, the item as a chip
        // with its type in words, and when, under the day's heading.
        var entry = cut.Find("[data-testid=recent-activity] li");
        Assert.Equal("Yesterday", cut.Find("[data-testid=recent-activity] .sum-day").TextContent.Trim());
        Assert.Equal(("OA", "true"), (entry.QuerySelector(".avatar")!.TextContent.Trim(), entry.QuerySelector(".avatar")!.GetAttribute("aria-hidden")));
        Assert.StartsWith("Omar Ali changed the priority from Medium to High on",
            System.Text.RegularExpressions.Regex.Replace(entry.QuerySelector(".sum-activity-text p")!.TextContent, @"\s+", " ").Trim(), StringComparison.Ordinal);
        var chip = entry.QuerySelector("a.sum-item-chip")!;
        Assert.Equal(("spaces/WEB/board?task=WEB-3", "Bug", "WEB-3: Refund shows twice"),
            (chip.GetAttribute("href"), chip.QuerySelector(".type-icon .sr-only")!.TextContent, chip.QuerySelector(".type-icon + span")!.TextContent));
        Assert.Equal("a day ago", entry.QuerySelector(".sum-when")!.TextContent.Trim());
        var progress = cut.Find("[role=progressbar]");
        Assert.Equal(("50", "Card refunds"), (progress.GetAttribute("aria-valuenow"), progress.GetAttribute("aria-label")));
        Assert.Contains("1 of 2 done", cut.Find("[data-testid=container-progress]").TextContent, StringComparison.Ordinal);
        Assert.Equal(["Omar Ali", "Unassigned"], cut.FindAll("[data-testid=team-workload] tbody th").Select(Heard));
    }

    /// <summary>The text without what is hidden from screen readers, such as an avatar's initials.</summary>
    private static string Heard(AngleSharp.Dom.IElement element)
    {
        var copy = (AngleSharp.Dom.IElement)element.Clone();
        foreach (var hidden in copy.QuerySelectorAll("[aria-hidden=true]").ToList())
        {
            hidden.Remove();
        }

        return copy.TextContent.Trim();
    }

    [Fact]
    public void An_empty_space_says_so_rather_than_showing_empty_boxes()
    {
        _summaries.Summary = _summaries.Summary with
        {
            Last7Days = new SummaryCounts(0, 0, 0, 0),
            StatusOverview = [new(StatusCategory.ToDo, 0), new(StatusCategory.InProgress, 0), new(StatusCategory.Done, 0)],
            RecentActivity = [],
            Types = [],
            Workload = [],
            Containers = [],
        };

        var cut = Show();

        Assert.Contains("No work items yet.", cut.Find("[data-testid=status-overview]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Nothing has changed in the last 30 days.", cut.Find("[data-testid=recent-activity]").TextContent, StringComparison.Ordinal);
        Assert.Contains("No epics yet.", cut.Find("[data-testid=container-progress]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_space_one_cannot_see_is_not_found()
    {
        _summaries.Fail = true;

        Assert.Contains("Not found", Show().Markup, StringComparison.Ordinal);
    }

    private sealed class FakeSummaries : ISpaceSummaryService
    {
        public bool Fail { get; set; }

        public SpaceSummary Summary { get; set; } = new("WEB", "Website Revamp", ProjectTemplate.Kanban, new SummaryCounts(2, 5, 4, 1),
            [new(StatusCategory.ToDo, 3), new(StatusCategory.InProgress, 1), new(StatusCategory.Done, 2)],
            [new("WEB-3", "Refund shows twice", WorkItemType.Bug, "Omar Ali", new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero),
                WorkItemField.Priority, "Medium", "High", null)],
            [new(Priority.High, 1), new(Priority.Medium, 5)],
            [new(WorkItemType.Bug, 1), new(WorkItemType.Task, 5)],
            [new(AssigneeRef.Of(Guid.NewGuid(), "Omar Ali", true), 3), new(null, 1)],
            [new("WEB-1", "Card refunds", WorkItemType.Epic, new Progress(1, 2))]);

        public Task<Result<SpaceSummary>> GetAsync(string spaceKey, DateOnly today, CancellationToken ct) =>
            Task.FromResult(Fail ? Result<SpaceSummary>.Fail(AppError.NotFound("space")) : Result<SpaceSummary>.Ok(Summary));
    }
}
