using System.Text.Json.Nodes;
using Upms.Domain.Identity;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Fixtures;

/// <summary>Formatted-text documents as the editor sends them (Phase 4 research R3).</summary>
public static class Documents
{
    /// <summary>Plain text as one paragraph per line.</summary>
    public static string Text(string text) => FormattedText.FromPlainText(text).Json;

    /// <summary>One paragraph: the text, then a mention of each person.</summary>
    public static string Mentioning(string text, params User[] people) =>
        Of(Paragraph([Run(text), .. people.Select(p => Mention(p.Id, p.DisplayName))]));

    public static string Of(params JsonNode[] blocks) =>
        new JsonObject { ["type"] = "doc", ["content"] = new JsonArray(blocks) }.ToJsonString();

    public static JsonObject Paragraph(params JsonNode[] inline) =>
        new() { ["type"] = "paragraph", ["content"] = new JsonArray(inline) };

    public static JsonObject Run(string text, params string[] marks) => marks.Length == 0
        ? new JsonObject { ["type"] = "text", ["text"] = text }
        : new JsonObject
        {
            ["type"] = "text",
            ["text"] = text,
            ["marks"] = new JsonArray(marks.Select(m => (JsonNode)new JsonObject { ["type"] = m }).ToArray()),
        };

    public static JsonObject Link(string text, string href) => new()
    {
        ["type"] = "text",
        ["text"] = text,
        ["marks"] = new JsonArray(new JsonObject { ["type"] = "link", ["attrs"] = new JsonObject { ["href"] = href } }),
    };

    public static JsonObject Mention(Guid id, string label) =>
        new() { ["type"] = "mention", ["attrs"] = new JsonObject { ["id"] = id.ToString(), ["label"] = label } };

    public static JsonObject Node(string type, params JsonNode[] content) =>
        new() { ["type"] = type, ["content"] = new JsonArray(content) };

    public static JsonObject Heading(int level, string text) => new()
    {
        ["type"] = "heading",
        ["attrs"] = new JsonObject { ["level"] = level },
        ["content"] = new JsonArray(Run(text)),
    };

    public static JsonObject Table(params string[][] rows) => Node("table",
        rows.Select((cells, r) => (JsonNode)Node("tableRow",
            cells.Select(c => (JsonNode)Node(r == 0 ? "tableHeader" : "tableCell", Paragraph(Run(c)))).ToArray())).ToArray());
}
