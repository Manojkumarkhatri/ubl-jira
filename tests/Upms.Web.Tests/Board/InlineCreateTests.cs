using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Upms.Web.Components.Pages.Board;

namespace Upms.Web.Tests.Board;

/// <summary>The "What needs to be done?" box (FR-018).</summary>
public sealed class InlineCreateTests : BunitTestBase
{
    private readonly List<string> _created = [];
    private string? _nextError;

    private IRenderedComponent<InlineCreate> RenderBox() => Render<InlineCreate>(p => p
        .Add(x => x.ColumnName, "To Do")
        .Add(x => x.OnCreate, title =>
        {
            _created.Add(title);
            return Task.FromResult(_nextError);
        }));

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
