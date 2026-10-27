using Bunit;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Board;

/// <summary>Jira-style cards: the summary first, then the details, each also in words (Jira parity pass US5 scenario 1,
/// FR-024; research R15).</summary>
public sealed class TaskCardTests : BunitTestBase
{
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly AssigneeRef Sara = AssigneeRef.Of(Guid.Parse("6f1d7c35-1c55-4b1d-9c41-0b6f5a1c2e01"), "Sara Khan", canWork: true);

    private IRenderedComponent<TaskCard> RenderCard(CardView card) => Render<TaskCard>(p => p
        .Add(x => x.Card, card)
        .Add(x => x.ProjectKey, "WEB")
        .Add(x => x.MoveTargets, [new ColumnOption(FakeBoardService.ToDo, "To Do")])
        .Add(x => x.Today, Today));

    private static CardView Story() =>
        new CardView("WEB-4", "Refund to card", Priority.High, FakeBoardService.ToDo, 2, 5, [1], Sara, new DateOnly(2026, 10, 9),
            WorkItemType.Story, 3, ["payments"], new ContainerView("WEB-1", "Card refunds", WorkItemType.Epic, EpicColor.Teal));

    [Fact]
    public void JP_US5_AS1_The_summary_comes_first_then_each_detail_with_its_words()
    {
        var cut = RenderCard(Story());

        var card = cut.Find("article.bcard");
        Assert.Equal("Refund to card", card.FirstElementChild!.TextContent.Trim());
        var text = card.TextContent;
        foreach (var words in new[] { "Story", "WEB-4", "High priority", "3 estimate", "2/5 sub-tasks done", "Due", "Epic: Card refunds",
            "Assigned to Sara Khan", "payments" })
        {
            Assert.Contains(words, text, StringComparison.Ordinal);
        }

        Assert.Equal("Labels", cut.Find("[data-testid=card-labels]").GetAttribute("aria-label"));
    }

    [Fact]
    public void JP_US5_AS1_The_epic_tag_uses_the_epics_color()
    {
        var cut = RenderCard(Story());

        var tag = cut.Find("[data-testid=epic-tag]");
        Assert.Contains("epic-teal", tag.ClassList);
        Assert.Contains("Card refunds", tag.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stage_tag_has_a_neutral_style()
    {
        var cut = RenderCard(Story() with { Container = new ContainerView("CBU-2", "Planning", WorkItemType.Stage) });

        var tag = cut.Find("[data-testid=epic-tag]");
        Assert.Contains("stage-tag", tag.ClassList);
        Assert.Contains("Stage: Planning", tag.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void JP_US5_AS1_A_sub_task_card_names_its_parent()
    {
        var card = FakeBoardService.Card("WEB-7", "Write the API", Priority.Medium, FakeBoardService.ToDo, type: WorkItemType.Subtask);
        var subtask = card with { ParentKey = "WEB-4" };

        var cut = RenderCard(subtask);

        Assert.Contains("Sub-task of WEB-4", cut.Find("[data-testid=card-parent]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_avatar_shows_initials_in_a_color_kept_for_the_person()
    {
        var first = RenderCard(Story()).Find(".avatar");
        var again = RenderCard(Story()).Find(".avatar");

        Assert.Equal("SK", first.TextContent.Trim());
        Assert.Equal("true", first.GetAttribute("aria-hidden"));
        Assert.Contains(first.ClassList, c => c.StartsWith("avatar-", StringComparison.Ordinal));
        Assert.Equal(first.ClassName, again.ClassName);
    }
}
