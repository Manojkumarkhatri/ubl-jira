using Bunit;
using Upms.Web.Components.Shared;

namespace Upms.Web.Tests.Shared;

/// <summary>User text is shown safely: line breaks kept, web links clickable, never markup (FR-025, FR-044, R10).</summary>
public sealed class PlainTextTests : BunitTestBase
{
    private IRenderedComponent<PlainText> Show(string? text) => Render<PlainText>(p => p.Add(x => x.Text, text));

    [Fact]
    public void Line_breaks_are_kept()
    {
        var cut = Show("First line\nSecond line");

        var block = cut.Find(".plain-text");
        Assert.Equal("First line\nSecond line", block.TextContent);
    }

    [Fact]
    public void Web_links_become_safe_links_that_open_in_a_new_tab()
    {
        var cut = Show("Brief: https://example.com/brief?x=1, and http://intranet.local/page.");

        var links = cut.FindAll("a");
        Assert.Equal(["https://example.com/brief?x=1", "http://intranet.local/page"], links.Select(a => a.GetAttribute("href")));
        Assert.All(links, a =>
        {
            Assert.Equal("_blank", a.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
        });
        Assert.Equal("Brief: https://example.com/brief?x=1, and http://intranet.local/page.", cut.Find(".plain-text").TextContent);
    }

    [Fact]
    public void Markup_is_shown_as_text_and_never_runs()
    {
        var cut = Show("<script>alert('x')</script><b>bold</b><img src=x onerror=alert(1)>");

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("b"));
        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("<script>alert('x')</script>", cut.Find(".plain-text").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("ftp://files.example.com")]
    [InlineData("see www.example.com")]
    public void Only_http_and_https_addresses_become_links(string text)
    {
        var cut = Show(text);

        Assert.Empty(cut.FindAll("a"));
        Assert.Equal(text, cut.Find(".plain-text").TextContent);
    }

    [Fact]
    public void Empty_text_renders_the_placeholder()
    {
        var cut = Render<PlainText>(p => p.Add(x => x.Text, null).Add(x => x.Placeholder, "No description yet."));

        Assert.Equal("No description yet.", cut.Find(".plain-text").TextContent);
    }
}
