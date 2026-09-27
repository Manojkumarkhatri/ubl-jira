using Bunit;
using Upms.Application.Work;
using Upms.Web.Components.Pages.Board;

namespace Upms.Web.Tests.Board;

/// <summary>The keyboard alternative to dragging (FR-019, FR-042).</summary>
public sealed class MoveToMenuTests : BunitTestBase
{
    private static readonly IReadOnlyList<ColumnOption> Columns =
        [new(1, "To Do"), new(2, "In Progress"), new(3, "Done")];

    private MoveRequest? _requested;

    private IRenderedComponent<MoveToMenu> RenderMenu() => Render<MoveToMenu>(p => p
        .Add(x => x.CardKey, "WEB-1")
        .Add(x => x.Columns, Columns)
        .Add(x => x.OnMove, request => _requested = request));

    [Fact]
    public void The_menu_is_a_labelled_disclosure_button()
    {
        var cut = RenderMenu();

        var button = cut.Find("button");
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Contains("WEB-1", button.TextContent, StringComparison.Ordinal);

        button.Click();

        Assert.Equal("true", cut.Find("button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void US1_AS8_Every_column_is_offered_with_its_top_and_bottom()
    {
        var cut = RenderMenu();
        cut.Find("button").Click();

        var options = cut.FindAll(".move-list button").Select(b => b.TextContent.Trim()).ToList();

        Assert.Equal(["To Do: top", "To Do: bottom", "In Progress: top", "In Progress: bottom", "Done: top", "Done: bottom"], options);
    }

    [Fact]
    public void US1_AS8_Choosing_a_target_requests_the_move_and_closes_the_menu()
    {
        var cut = RenderMenu();
        cut.Find("button").Click();

        cut.FindAll(".move-list button").Single(b => b.TextContent.Trim() == "Done: bottom").Click();

        Assert.Equal(new MoveRequest(3, CardPlacement.AtEnd), _requested);
        Assert.Empty(cut.FindAll(".move-list"));
    }

    [Fact]
    public void Escape_closes_the_menu()
    {
        var cut = RenderMenu();
        cut.Find("button").Click();

        cut.Find(".move-list").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".move-list"));
    }
}
