using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Upms.Domain.Work;
using Upms.Web.Components.Pages.Board;

namespace Upms.Web.Tests.Board;

/// <summary>The "What needs to be done?" box (FR-018).</summary>
public sealed class InlineCreateTests : BunitTestBase
{
    private readonly List<string> _created = [];
    private readonly List<WorkItemType> _types = [];
    private string? _nextError;

    private IRenderedComponent<InlineCreate> RenderBox(IReadOnlyList<WorkItemType>? types = null,
        WorkItemType defaultType = WorkItemType.Task) => Render<InlineCreate>(p => p
        .Add(x => x.ColumnName, "To Do")
        .Add(x => x.Types, types ?? [])
        .Add(x => x.DefaultType, defaultType)
        .Add(x => x.OnCreate, (title, type) =>
        {
            _created.Add(title);
            _types.Add(type);
            return Task.FromResult(_nextError);
        }));

    [Fact]
    public void P3_FR010_The_box_offers_the_types_and_starts_on_the_default()
    {
        var cut = RenderBox([WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug], WorkItemType.Story);

        var choice = cut.Find("select.type-choice");
        Assert.Equal(["Story", "Task", "Bug"], choice.QuerySelectorAll("option").Select(o => o.TextContent.Trim()));
        Assert.Equal("Story", choice.GetAttribute("value"));
        Assert.Contains("Type", cut.Find($"label[for='{choice.Id}']").TextContent, StringComparison.Ordinal);

        choice.Change("Bug");
        cut.Find("input").Input("Refund shows twice");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([WorkItemType.Bug], _types);
        Assert.Equal("Bug", cut.Find("select.type-choice").GetAttribute("value")); // the next item keeps the choice
    }

    [Fact]
    public void With_one_type_there_is_no_choice()
    {
        var cut = RenderBox([WorkItemType.Task]);

        Assert.Empty(cut.FindAll("select.type-choice"));
        cut.Find("input").Input("Collect quotes");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([WorkItemType.Task], _types);
    }

    [Fact]
    public void The_box_is_labelled_for_its_column()
    {
        var cut = RenderBox();

        var input = cut.Find("input");
        Assert.Equal("What needs to be done?", input.GetAttribute("placeholder"));
        Assert.Contains("To Do", cut.Find($"label[for='{input.Id}']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void US1_AS5_Enter_creates_the_task_then_the_box_is_empty_and_keeps_focus()
    {
        var cut = RenderBox();

        cut.Find("input").Input("  Design the home page ");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Design the home page"], _created);
        cut.WaitForAssertion(() => Assert.Equal("", cut.Find("input").GetAttribute("value")));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void Enter_in_an_empty_box_creates_nothing(string text)
    {
        var cut = RenderBox();

        cut.Find("input").Input(text);
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(_created);
    }

    [Fact]
    public void Escape_clears_the_box()
    {
        var cut = RenderBox();

        cut.Find("input").Input("Half a thought");
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("", cut.Find("input").GetAttribute("value"));
        Assert.Empty(_created);
    }

    [Fact]
    public void A_refused_title_keeps_the_text_and_shows_why()
    {
        _nextError = "The title can have at most 255 characters.";
        var cut = RenderBox();
        var longTitle = new string('x', 256);

        cut.Find("input").Input(longTitle);
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(longTitle, cut.Find("input").GetAttribute("value"));
        Assert.Contains("255", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
    }
}
