using System.Text.Json.Nodes;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Formatted text is the editor's JSON document, checked against an allowlist before it is kept (Phase 4
/// FR-001–FR-004, data model, research R3).</summary>
public sealed class FormattedTextTests
{
    private static readonly Guid Bilal = Guid.Parse("0000000b-0000-0000-0000-000000000000");
    private static readonly Guid Omar = Guid.Parse("0000000c-0000-0000-0000-000000000000");

    private static string Doc(params JsonNode[] blocks) => new JsonObject { ["type"] = "doc", ["content"] = new JsonArray(blocks) }.ToJsonString();

    private static JsonObject Node(string type, params JsonNode[] content) => content.Length == 0
        ? new JsonObject { ["type"] = type }
        : new JsonObject { ["type"] = type, ["content"] = new JsonArray(content) };

    private static JsonObject Text(string text, params JsonNode[] marks) => marks.Length == 0
        ? new JsonObject { ["type"] = "text", ["text"] = text }
        : new JsonObject { ["type"] = "text", ["text"] = text, ["marks"] = new JsonArray(marks) };

    private static JsonObject Mark(string type) => new() { ["type"] = type };

    private static JsonObject Link(string href) => new() { ["type"] = "link", ["attrs"] = new JsonObject { ["href"] = href, ["target"] = "_blank" } };

    private static JsonObject Paragraph(string text) => Node("paragraph", Text(text));

    private static JsonObject Mention(Guid id, string label) =>
        new() { ["type"] = "mention", ["attrs"] = new JsonObject { ["id"] = id.ToString(), ["label"] = label } };

    private static JsonObject Table(int rows, int columns, bool header = false) => Node("table",
        Enumerable.Range(0, rows).Select(r => (JsonNode)Node("tableRow",
            Enumerable.Range(0, columns).Select(c => (JsonNode)Node(header && r == 0 ? "tableHeader" : "tableCell", Paragraph($"r{r}c{c}"))).ToArray()))
        .ToArray());

    private static FormattedText Parse(string json) => FormattedText.Parse(json, "Description").Value
        ?? throw new InvalidOperationException(FormattedText.Parse(json, "Description").Error!.Message);

    private static string? ErrorOf(string json) => FormattedText.Parse(json, "Body").Error?.Code;

    [Fact]
    public void P4_FR001_Every_allowed_node_and_mark_is_kept()
    {
        var json = Doc(
            new JsonObject { ["type"] = "heading", ["attrs"] = new JsonObject { ["level"] = 2 }, ["content"] = new JsonArray(Text("Plan")) },
            Node("paragraph", Text("bold", Mark("bold")), Text("italic", Mark("italic")), Text("strike", Mark("strike")),
                Text("code", Mark("code")), Node("hardBreak"), Text("docs", Link("https://example.com/docs"))),
            Node("bulletList", Node("listItem", Paragraph("one"))),
            new JsonObject { ["type"] = "orderedList", ["attrs"] = new JsonObject { ["start"] = 3 }, ["content"] = new JsonArray(Node("listItem", Paragraph("three"))) },
            Node("blockquote", Paragraph("quoted")),
            Node("codeBlock", Text("var x = 1;")),
            Table(2, 2, header: true),
            Node("paragraph", Mention(Bilal, "Bilal Tester")),
            new JsonObject { ["type"] = "image", ["attrs"] = new JsonObject { ["attachmentId"] = 42, ["alt"] = "Screenshot" } });

        var text = Parse(json);

        var kept = JsonNode.Parse(text.Json)!.AsObject();
        var blocks = kept["content"]!.AsArray().Select(b => b!["type"]!.GetValue<string>());
        Assert.Equal(["heading", "paragraph", "bulletList", "orderedList", "blockquote", "codeBlock", "table", "paragraph", "image"], blocks);
        Assert.Equal(2, kept["content"]![0]!["attrs"]!["level"]!.GetValue<int>());
        Assert.Equal(3, kept["content"]![3]!["attrs"]!["start"]!.GetValue<int>());
        Assert.Equal("https://example.com/docs", kept["content"]![1]!["content"]![5]!["marks"]![0]!["attrs"]!["href"]!.GetValue<string>());
        Assert.Equal(text.Json, Parse(text.Json).Json);
    }

    [Fact]
    public void Attributes_outside_the_allowlist_are_dropped_and_cell_spans_kept_within_limits()
    {
        var cell = Node("tableCell", Paragraph("wide"));
        cell["attrs"] = new JsonObject { ["colspan"] = 3, ["rowspan"] = 2, ["colwidth"] = new JsonArray(200), ["background"] = "url(javascript:x)" };
        var code = Node("codeBlock", Text("x"));
        code["attrs"] = new JsonObject { ["language"] = "js" };
        var paragraph = Paragraph("left");
        paragraph["attrs"] = new JsonObject { ["textAlign"] = "left", ["class"] = "x", ["style"] = "color:red", ["size"] = "huge" };
        var bullets = Node("bulletList", Node("listItem", Paragraph("b")));
        bullets["attrs"] = new JsonObject { ["listStyle"] = "url(x)" };

        var text = Parse(Doc(paragraph, code, Node("table", Node("tableRow", cell)), Node("paragraph", Text("a", Link("https://example.com"))), bullets));

        Assert.DoesNotContain("language", text.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("style", text.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("size", text.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("target", text.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript", text.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("colwidth", text.Json, StringComparison.Ordinal); // one width for three columns
        var kept = Assert.Single(Assert.Single(text.Root.Content[2].Content).Content);
        Assert.Equal((3, 2), (kept.Colspan, kept.Rowspan));
        Assert.Null(kept.Background);
        Assert.Null(text.Root.Content[4].ListStyle);
    }

    [Fact]
    public void Jira_text_styles_lists_checklists_colours_and_merged_cells_are_kept()
    {
        JsonObject With(JsonObject node, JsonObject attrs)
        {
            node["attrs"] = attrs;
            return node;
        }

        var color = new JsonObject { ["type"] = "textColor", ["attrs"] = new JsonObject { ["color"] = "green" } };
        var json = Doc(
            With(Node("heading", Text("Six")), new JsonObject { ["level"] = 6 }),
            With(Node("paragraph", Text("small")), new JsonObject { ["size"] = "small" }),
            Node("paragraph", Text("u", Mark("underline")), Text("2", Mark("subscript")), Text("n", Mark("superscript")), Text("green", color)),
            With(Node("bulletList", Node("listItem", Paragraph("square"))), new JsonObject { ["listStyle"] = "square" }),
            With(Node("orderedList", Node("listItem", Paragraph("roman"))), new JsonObject { ["start"] = 1, ["type"] = "i" }),
            Node("taskList", With(Node("taskItem", Paragraph("done")), new JsonObject { ["checked"] = true }), With(Node("taskItem", Paragraph("open")), new JsonObject { ["checked"] = false })),
            Node("table", Node("tableRow", With(Node("tableHeader", Paragraph("h")), new JsonObject { ["colspan"] = 2, ["rowspan"] = 1, ["colwidth"] = new JsonArray(100, 140), ["background"] = "blue" }))));

        var text = Parse(json);
        var again = Parse(text.Json);

        Assert.Equal(text.Json, again.Json);
        Assert.Equal(6, text.Root.Content[0].Level);
        Assert.Equal("small", text.Root.Content[1].Size);
        Assert.Equal(["underline", "subscript", "superscript", "textColor"], text.Root.Content[2].Content.Select(t => t.Marks.Single().Type));
        Assert.Equal("green", text.Root.Content[2].Content[3].Marks.Single().Color);
        Assert.Equal("square", text.Root.Content[3].ListStyle);
        Assert.Equal("i", text.Root.Content[4].ListStyle);
        Assert.Equal([true, false], text.Root.Content[5].Content.Select(i => i.Checked == true));
        var header = text.Root.Content[6].Content[0].Content[0];
        Assert.Equal((2, "blue"), (header.Colspan, header.Background));
        Assert.Equal([100, 140], header.ColumnWidths!);
        Assert.Contains("done", text.PlainText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"type":"doc","content":[{"type":"iframe","attrs":{"src":"https://evil"}}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"horizontalRule"}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"u","marks":[{"type":"highlight"}]}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"c","marks":[{"type":"textColor","attrs":{"color":"#ff0000"}}]}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"taskItem","content":[{"type":"paragraph"}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"table","content":[{"type":"tableRow","content":[{"type":"tableCell","attrs":{"colspan":21},"content":[{"type":"paragraph"}]}]}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"s","marks":[{"type":"textStyle","attrs":{"color":"red"}}]}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"heading","attrs":{"level":7},"content":[{"type":"text","text":"h7"}]}]}""")]
    [InlineData("""{"type":"paragraph","content":[{"type":"text","text":"not a doc"}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"tableRow","content":[]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"bulletList","content":[{"type":"paragraph"}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"mention","attrs":{"id":"not-a-guid","label":"x"}}]}]}""")]
    [InlineData("not json")]
    [InlineData("")]
    public void P4_FR003_Unknown_or_malformed_content_is_refused(string json)
    {
        Assert.Equal(FormattedText.UnsupportedFormattingCode, ErrorOf(json));
    }

    [Fact]
    public void Tables_do_not_nest_and_have_at_most_50_rows_and_20_columns()
    {
        var nested = Node("table", Node("tableRow", Node("tableCell", Table(1, 1))));

        Assert.Equal(FormattedText.UnsupportedFormattingCode, ErrorOf(Doc(nested)));
        Assert.Equal(FormattedText.UnsupportedFormattingCode, ErrorOf(Doc(Table(51, 1))));
        Assert.Equal(FormattedText.UnsupportedFormattingCode, ErrorOf(Doc(Table(1, 21))));
        Assert.Null(ErrorOf(Doc(Table(50, 20))));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox")]
    [InlineData("ftp://example.com/file")]
    [InlineData("/relative/path")]
    [InlineData("  ")]
    public void P4_FR003_Links_other_than_http_https_and_mailto_are_refused(string href)
    {
        Assert.Equal(FormattedText.InvalidLinkCode, ErrorOf(Doc(Node("paragraph", Text("x", Link(href))))));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("HTTPS://example.com/a?b=c#d")]
    [InlineData("mailto:amina@example.com")]
    public void Http_https_and_mailto_links_are_kept(string href)
    {
        Assert.Null(ErrorOf(Doc(Node("paragraph", Text("x", Link(href))))));
    }

    [Fact]
    public void A_link_over_2000_characters_is_refused()
    {
        Assert.Equal(FormattedText.InvalidLinkCode, ErrorOf(Doc(Node("paragraph", Text("x", Link("https://example.com/" + new string('a', 2000)))))));
    }

    [Fact]
    public void P4_FR002_Text_is_limited_to_32000_characters_and_the_document_to_512_KB()
    {
        var error = FormattedText.Parse(Doc(Paragraph(new string('a', 32_001))), "Description").Error!;
        Assert.Equal((FormattedText.TextTooLongCode, "Description"), (error.Code, error.Field));
        Assert.Null(ErrorOf(Doc(Paragraph(new string('a', 32_000)))));

        // Under 32,000 characters of text, but over 512 KB of document (marks on every character).
        var heavy = Enumerable.Range(0, 6_000).Select(_ => (JsonNode)Text("ab", Mark("bold"), Mark("italic"), Mark("strike"), Link("https://example.com/x")));
        Assert.Equal(FormattedText.TextTooLongCode, ErrorOf(Doc(Node("paragraph", heavy.ToArray()))));
    }

    [Fact]
    public void Plain_text_puts_line_breaks_between_blocks_and_table_cells()
    {
        var text = Parse(Doc(
            Paragraph("First"),
            Node("paragraph", Text("line"), Node("hardBreak"), Text("break")),
            Node("bulletList", Node("listItem", Paragraph("item"))),
            Table(1, 2),
            Node("paragraph", Text("Hi "), Mention(Bilal, "Bilal Tester"))));

        Assert.Equal("First\nline\nbreak\nitem\nr0c0\nr0c1\nHi @Bilal Tester", text.PlainText);
    }

    [Fact]
    public void Mention_and_image_ids_are_listed_once_each()
    {
        var image = new JsonObject { ["type"] = "image", ["attrs"] = new JsonObject { ["attachmentId"] = 7 } };
        var text = Parse(Doc(Node("paragraph", Mention(Bilal, "Bilal"), Mention(Omar, "Omar"), Mention(Bilal, "Bilal")), image));

        Assert.Equal([Bilal, Omar], text.MentionIds);
        Assert.Equal([7L], text.ImageAttachmentIds);
    }

    [Fact]
    public void Mentions_of_people_outside_the_set_become_plain_text_and_other_images_go()
    {
        var image = new JsonObject { ["type"] = "image", ["attrs"] = new JsonObject { ["attachmentId"] = 7 } };
        var text = Parse(Doc(Node("paragraph", Text("Ask "), Mention(Bilal, "Bilal"), Text(" and "), Mention(Omar, "Omar")), image));

        var kept = text.Restrict(new HashSet<Guid> { Bilal }, new HashSet<long>());

        Assert.Equal([Bilal], kept.MentionIds);
        Assert.Empty(kept.ImageAttachmentIds);
        Assert.Equal("Ask @Bilal and @Omar", kept.PlainText);
    }

    [Fact]
    public void P4_FR010_Mentions_take_the_current_name_and_keep_the_stored_one_otherwise()
    {
        var text = Parse(Doc(Node("paragraph", Mention(Bilal, "Bilal"), Text(" and "), Mention(Omar, "Omar"))));

        var shown = text.WithMentionNames(new Dictionary<Guid, string> { [Bilal] = "Bilal Khan" });

        Assert.Equal("@Bilal Khan and @Omar", shown.PlainText);
        Assert.Equal([Bilal, Omar], shown.MentionIds);
    }

    [Fact]
    public void P4_FR004_Old_plain_text_becomes_one_paragraph_per_line()
    {
        var text = FormattedText.FromPlainText("First line\r\nSecond line\n\nAfter a blank line https://example.com");

        Assert.Equal(["paragraph", "paragraph", "paragraph", "paragraph"], text.Root.Content.Select(n => n.Type));
        Assert.Equal("First line\nSecond line\n\nAfter a blank line https://example.com", text.PlainText);
        Assert.Equal(text.Json, Parse(text.Json).Json);
    }

    [Fact]
    public void An_empty_document_is_empty()
    {
        Assert.True(Parse(Doc(Node("paragraph"))).IsEmpty);
        Assert.True(Parse("""{"type":"doc"}""").IsEmpty);
        Assert.False(Parse(Doc(Paragraph("x"))).IsEmpty);
    }
}
