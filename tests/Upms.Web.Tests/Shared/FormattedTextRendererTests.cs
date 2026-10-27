using AngleSharp.Html.Parser;
using Bunit;
using Upms.Application.Work;
using Upms.Domain.Work;
using Upms.Web.Components.Shared;

namespace Upms.Web.Tests.Shared;

/// <summary>Formatted text is rendered by the server from the checked document, encoding every text node (Phase 4 FR-001,
/// FR-003, FR-005, research R3).</summary>
public sealed class FormattedTextRendererTests : BunitTestBase
{
    private static string Html(string documentJson) =>
        FormattedTextRenderer.ToHtml(FormattedText.Parse(documentJson, "Body").Value
            ?? throw new InvalidOperationException("The test document was refused."));

    private static string Doc(string blocks) => $$"""{"type":"doc","content":[{{blocks}}]}""";

    private static string Para(string inline) => $$"""{"type":"paragraph","content":[{{inline}}]}""";

    private static string Text(string text, string marks = "") =>
        marks.Length == 0 ? $$"""{"type":"text","text":"{{text}}"}""" : $$"""{"type":"text","text":"{{text}}","marks":[{{marks}}]}""";

    [Theory]
    [InlineData("""{"type":"bold"}""", "<strong>x</strong>")]
    [InlineData("""{"type":"italic"}""", "<em>x</em>")]
    [InlineData("""{"type":"strike"}""", "<s>x</s>")]
    [InlineData("""{"type":"code"}""", "<code>x</code>")]
    public void Each_mark_renders_as_its_element(string mark, string expected)
    {
        Assert.Equal($"<p>{expected}</p>", Html(Doc(Para(Text("x", mark)))));
    }

    [Fact]
    public void Marks_combine()
    {
        Assert.Equal("<p><strong><em>x</em></strong></p>", Html(Doc(Para(Text("x", """{"type":"bold"},{"type":"italic"}""")))));
    }

    [Fact]
    public void Links_open_in_a_new_tab_without_passing_on_the_page()
    {
        var html = Html(Doc(Para(Text("brief", """{"type":"link","attrs":{"href":"https://example.com/a?b=1&c=2"}}"""))));

        Assert.Equal("""<p><a href="https://example.com/a?b=1&amp;c=2" target="_blank" rel="noopener noreferrer nofollow">brief</a></p>""", html);
    }

    [Theory]
    [InlineData(1, "h4")]
    [InlineData(2, "h5")]
    [InlineData(3, "h6")]
    [InlineData(4, "h6")]
    [InlineData(6, "h6")]
    public void Headings_sit_below_the_drawers_own_headings(int level, string element)
    {
        // Below h6 the level keeps its size by class.
        Assert.Equal($"<{element} class=\"ft-h{level}\">Plan</{element}>", Html(Doc($$"""{"type":"heading","attrs":{"level":{{level}}},"content":[{{Text("Plan")}}]}""")));
    }

    [Fact]
    public void Lists_quotes_breaks_and_code_blocks_render()
    {
        var html = Html(Doc(string.Join(',',
            """{"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"a"}]}]}]}""",
            """{"type":"orderedList","attrs":{"start":3},"content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"b"}]}]}]}""",
            """{"type":"blockquote","content":[{"type":"paragraph","content":[{"type":"text","text":"c"},{"type":"hardBreak"},{"type":"text","text":"d"}]}]}""",
            """{"type":"codeBlock","content":[{"type":"text","text":"if (a < b) {}"}]}""",
            """{"type":"paragraph"}""")));

        Assert.Equal("<ul><li><p>a</p></li></ul><ol start=\"3\"><li><p>b</p></li></ol><blockquote><p>c<br>d</p></blockquote>"
            + "<pre><code>if (a &lt; b) {}</code></pre><p><br></p>", html);
    }

    [Fact]
    public void P4_FR005_Header_rows_render_as_column_headers()
    {
        var html = Html(Doc("""
            {"type":"table","content":[
              {"type":"tableRow","content":[{"type":"tableHeader","content":[{"type":"paragraph","content":[{"type":"text","text":"Step"}]}]},
                                            {"type":"tableHeader","content":[{"type":"paragraph","content":[{"type":"text","text":"Owner"}]}]}]},
              {"type":"tableRow","content":[{"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"Venue"}]}]},
                                            {"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"Amina"}]}]}]}]}
            """));

        var table = new HtmlParser().ParseDocument(html).QuerySelector("table")!;
        Assert.Equal(["Step", "Owner"], table.QuerySelectorAll("thead th").Select(th => th.TextContent));
        Assert.All(table.QuerySelectorAll("thead th"), th => Assert.Equal("col", th.GetAttribute("scope")));
        Assert.Equal(["Venue", "Amina"], table.QuerySelectorAll("tbody td").Select(td => td.TextContent));
        Assert.Equal("ft-table", table.ParentElement!.ClassName);
    }

    [Fact]
    public void Mentions_show_the_name_the_server_put_in_the_document()
    {
        var html = Html(Doc(Para("""{"type":"mention","attrs":{"id":"0000000b-0000-0000-0000-000000000000","label":"Bilal Khan"}}""")));

        Assert.Equal("<p><span class=\"mention\">@Bilal Khan</span></p>", html);
    }

    [Fact]
    public void P4_FR023_Images_show_their_preview_once_checked_and_say_so_otherwise()
    {
        var document = FormattedText.Parse(Doc("""
            {"type":"image","attrs":{"attachmentId":1,"alt":"Shot \"1\""}},
            {"type":"image","attrs":{"attachmentId":2,"alt":"New shot"}},
            {"type":"image","attrs":{"attachmentId":3}}
            """), "Body").Value!;

        var html = FormattedTextRenderer.ToHtml(document, id => id switch { 1 => ImageState.Ready, 2 => ImageState.Checking, _ => ImageState.Missing });

        Assert.Equal("<p class=\"ft-image\"><img src=\"attachments/1/preview\" alt=\"Shot &quot;1&quot;\" loading=\"lazy\"></p>"
            + "<p class=\"ft-image-note\">Image “New shot” is being checked for malware.</p>"
            + "<p class=\"ft-image-note\">Image removed.</p>", html);
    }

    [Fact]
    public void Without_attachment_states_images_are_never_loaded()
    {
        var document = FormattedText.Parse(Doc("""{"type":"image","attrs":{"attachmentId":1}}"""), "Body").Value!;

        Assert.Equal("<p class=\"ft-image-note\">Image removed.</p>", FormattedTextRenderer.ToHtml(document));
    }

    [Fact]
    public void The_view_shows_images_from_the_items_attachments()
    {
        var cut = Render<FormattedTextView>(p => p
            .Add(x => x.Text, new RichText(TextFormat.Document, Doc("""{"type":"image","attrs":{"attachmentId":7,"alt":"Shot"}}"""), ""))
            .Add(x => x.Attachments, [new AttachmentView(7, "shot.png", 10, "Amina Khan", DateTimeOffset.UtcNow, AttachmentCheckStatus.Passed, true, false)]));

        Assert.Equal("attachments/7/preview", cut.Find(".formatted-text img").GetAttribute("src"));
    }

    [Fact]
    public void Text_is_encoded()
    {
        Assert.Equal("<p>&lt;b&gt;x&lt;/b&gt; &amp; &quot;y&quot;</p>", Html(Doc(Para(Text("<b>x</b> & \\\"y\\\"")))));
    }

    [Fact]
    public void The_view_shows_documents_as_formatted_text()
    {
        var cut = Render<FormattedTextView>(p => p.Add(x => x.Text,
            new RichText(TextFormat.Document, Doc(Para(Text("Plan", """{"type":"bold"}"""))), "Plan")));

        Assert.Equal("Plan", cut.Find(".formatted-text strong").TextContent);
    }

    [Fact]
    public void P4_FR004_The_view_shows_old_plain_text_as_before()
    {
        var cut = Render<FormattedTextView>(p => p.Add(x => x.Text,
            new RichText(TextFormat.Plain, "Line one\nSee https://example.com", "Line one\nSee https://example.com")));

        Assert.Equal("Line one\nSee https://example.com", cut.Find(".plain-text").TextContent);
        Assert.Equal("https://example.com", cut.Find(".plain-text a").GetAttribute("href"));
    }

    [Fact]
    public void The_view_shows_the_placeholder_without_text()
    {
        var cut = Render<FormattedTextView>(p => p.Add(x => x.Text, null).Add(x => x.Placeholder, "No description yet."));

        Assert.Equal("No description yet.", cut.Find(".plain-text.is-empty").TextContent.Trim());
    }
}
