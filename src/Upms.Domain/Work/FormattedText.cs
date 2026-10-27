using System.Buffers;
using System.Text;
using System.Text.Json;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A description or comment in the editor's JSON document form, checked against an allowlist (Phase 4 FR-001–
/// FR-004, data model, research R3). Only the nodes, marks and attributes listed here survive; anything else is refused
/// or dropped, so rendering it can never run anything a user wrote.</summary>
public sealed class FormattedText
{
    public const int PlainTextMaxLength = 32_000;
    public const int DocumentMaxBytes = 512 * 1024;
    public const int MaxTableRows = 50;
    public const int MaxTableColumns = 20;
    public const int LinkMaxLength = 2_000;
    public const int LabelMaxLength = 100;
    public const int AltMaxLength = 200;
    public const string UnsupportedFormattingCode = "UnsupportedFormatting";
    public const string InvalidLinkCode = "InvalidLink";
    public const string TextTooLongCode = "TextTooLong";

    /// <summary>The colours text and table cells can take, by name (Jira's text colours).</summary>
    public static readonly IReadOnlyList<string> Colors = ["gray", "red", "orange", "yellow", "green", "teal", "blue", "purple"];

    /// <summary>Bullets other than discs, and numbering other than 1, 2, 3.</summary>
    public static readonly IReadOnlyList<string> BulletStyles = ["circle", "square"];

    public static readonly IReadOnlyList<string> NumberStyles = ["a", "A", "i", "I"];

    public const int MaxColumnWidth = 4_000;

    private static readonly HashSet<string> BlockTypes =
        ["paragraph", "heading", "bulletList", "orderedList", "taskList", "blockquote", "codeBlock", "table", "image"];

    private static readonly HashSet<string> BlockParents = ["doc", "blockquote", "listItem", "taskItem", "tableCell", "tableHeader"];

    private static readonly HashSet<string> MarkTypes = ["bold", "italic", "underline", "strike", "code", "subscript", "superscript", "textColor", "link"];

    private FormattedText(FormattedNode root)
    {
        Root = root;
        Json = Serialize(root);
        var lines = new List<string>();
        var mentions = new List<Guid>();
        var images = new List<long>();
        Collect(root, lines, mentions, images);
        PlainText = string.Join('\n', lines);
        MentionIds = mentions.Distinct().ToList();
        ImageAttachmentIds = images.Distinct().ToList();
    }

    /// <summary>The checked document; its type is <c>doc</c>.</summary>
    public FormattedNode Root { get; }

    /// <summary>The checked document as compact JSON, as it is stored.</summary>
    public string Json { get; }

    /// <summary>The text alone, with a line break between blocks and table cells: for length, history and search.</summary>
    public string PlainText { get; }

    /// <summary>The people mentioned, in order of first mention.</summary>
    public IReadOnlyList<Guid> MentionIds { get; }

    /// <summary>The attachments shown as images, in order.</summary>
    public IReadOnlyList<long> ImageAttachmentIds { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(PlainText) && ImageAttachmentIds.Count == 0;

    /// <summary>Checks a document from the editor.</summary>
    /// <param name="field">The field named in errors: <c>Description</c> or <c>Body</c>.</param>
    public static DomainResult<FormattedText> Parse(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unsupported(field);
        }

        if (Encoding.UTF8.GetByteCount(json) > DocumentMaxBytes)
        {
            return TooLong(field);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return Unsupported(field);
        }

        using (document)
        {
            var reader = new Reader(field);
            var root = reader.Read(document.RootElement, parent: null, inTable: false);
            if (reader.Error is { } error)
            {
                return error;
            }

            if (root is not { Type: "doc" })
            {
                return Unsupported(field);
            }

            var text = new FormattedText(root);
            return text.PlainText.Length > PlainTextMaxLength || Encoding.UTF8.GetByteCount(text.Json) > DocumentMaxBytes
                ? TooLong(field)
                : text;
        }
    }

    /// <summary>Phase 1–3 plain text as a document: one paragraph per line (FR-004).</summary>
    public static FormattedText FromPlainText(string? text)
    {
        var lines = (text ?? "").ReplaceLineEndings("\n").Split('\n');
        return new FormattedText(new FormattedNode("doc")
        {
            Content = lines.Select(line => line.Length == 0
                ? new FormattedNode("paragraph")
                : new FormattedNode("paragraph") { Content = [new FormattedNode("text") { Text = line }] }).ToList(),
        });
    }

    /// <summary>The same text with mentions of anyone outside <paramref name="mentionable"/> turned into plain text, and
    /// images of anything outside <paramref name="images"/> removed (research R3, R6).</summary>
    public FormattedText Restrict(IReadOnlySet<Guid> mentionable, IReadOnlySet<long> images)
    {
        FormattedNode? Keep(FormattedNode node) => node.Type switch
        {
            "mention" when !mentionable.Contains(node.MentionId!.Value) => new FormattedNode("text") { Text = "@" + node.Label },
            "image" when !images.Contains(node.AttachmentId!.Value) => null,
            _ => node.Content.Count == 0 ? node : node with { Content = node.Content.Select(Keep).OfType<FormattedNode>().ToList() },
        };

        return new FormattedText(Keep(Root)!);
    }

    /// <summary>The same text with each mention showing the name in <paramref name="names"/>, and its stored name otherwise
    /// (FR-010: mentions show the person's current name).</summary>
    public FormattedText WithMentionNames(IReadOnlyDictionary<Guid, string> names)
    {
        FormattedNode Rename(FormattedNode node) => node.Type == "mention" && names.TryGetValue(node.MentionId!.Value, out var name)
            ? node with { Label = name }
            : node.Content.Count == 0 ? node : node with { Content = node.Content.Select(Rename).ToList() };

        return MentionIds.Count == 0 ? this : new FormattedText(Rename(Root));
    }

    private static DomainError Unsupported(string field) =>
        new(UnsupportedFormattingCode, "This text has formatting that can't be kept. Remove it and try again.", field);

    private static DomainError TooLong(string field) =>
        new(TextTooLongCode, $"The text can have at most {PlainTextMaxLength:N0} characters.", field);

    private static void Collect(FormattedNode node, List<string> lines, List<Guid> mentions, List<long> images)
    {
        switch (node.Type)
        {
            case "paragraph" or "heading" or "codeBlock":
                var line = new StringBuilder();
                foreach (var inline in node.Content)
                {
                    switch (inline.Type)
                    {
                        case "text":
                            line.Append(inline.Text);
                            break;
                        case "hardBreak":
                            line.Append('\n');
                            break;
                        case "mention":
                            mentions.Add(inline.MentionId!.Value);
                            line.Append('@').Append(inline.Label);
                            break;
                    }
                }

                lines.Add(line.ToString());
                break;
            case "image":
                images.Add(node.AttachmentId!.Value);
                break;
            default:
                foreach (var child in node.Content)
                {
                    Collect(child, lines, mentions, images);
                }

                break;
        }
    }

    private static string Serialize(FormattedNode root)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, root);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void Write(Utf8JsonWriter writer, FormattedNode node)
    {
        writer.WriteStartObject();
        writer.WriteString("type", node.Type);
        switch (node.Type)
        {
            case "heading":
                writer.WriteStartObject("attrs");
                writer.WriteNumber("level", node.Level ?? 1);
                writer.WriteEndObject();
                break;
            case "orderedList" when node.Start is { } and not 1 || node.ListStyle is not null:
                writer.WriteStartObject("attrs");
                writer.WriteNumber("start", node.Start ?? 1);
                if (node.ListStyle is { } numbering)
                {
                    writer.WriteString("type", numbering);
                }

                writer.WriteEndObject();
                break;
            case "bulletList" when node.ListStyle is { } bullets:
                writer.WriteStartObject("attrs");
                writer.WriteString("listStyle", bullets);
                writer.WriteEndObject();
                break;
            case "paragraph" when node.Size is { } size:
                writer.WriteStartObject("attrs");
                writer.WriteString("size", size);
                writer.WriteEndObject();
                break;
            case "taskItem":
                writer.WriteStartObject("attrs");
                writer.WriteBoolean("checked", node.Checked == true);
                writer.WriteEndObject();
                break;
            case "tableCell" or "tableHeader" when node.Colspan is not null || node.Rowspan is not null || node.ColumnWidths is not null || node.Background is not null:
                writer.WriteStartObject("attrs");
                writer.WriteNumber("colspan", node.Colspan ?? 1);
                writer.WriteNumber("rowspan", node.Rowspan ?? 1);
                if (node.ColumnWidths is { } widths)
                {
                    writer.WriteStartArray("colwidth");
                    foreach (var width in widths)
                    {
                        writer.WriteNumberValue(width);
                    }

                    writer.WriteEndArray();
                }

                if (node.Background is { } background)
                {
                    writer.WriteString("background", background);
                }

                writer.WriteEndObject();
                break;
            case "mention":
                writer.WriteStartObject("attrs");
                writer.WriteString("id", node.MentionId!.Value);
                writer.WriteString("label", node.Label);
                writer.WriteEndObject();
                break;
            case "image":
                writer.WriteStartObject("attrs");
                writer.WriteNumber("attachmentId", node.AttachmentId!.Value);
                if (node.Alt is { } alt)
                {
                    writer.WriteString("alt", alt);
                }

                writer.WriteEndObject();
                break;
            case "text":
                writer.WriteString("text", node.Text);
                if (node.Marks.Count > 0)
                {
                    writer.WriteStartArray("marks");
                    foreach (var mark in node.Marks)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", mark.Type);
                        if (mark.Href is { } href)
                        {
                            writer.WriteStartObject("attrs");
                            writer.WriteString("href", href);
                            writer.WriteEndObject();
                        }
                        else if (mark.Color is { } color)
                        {
                            writer.WriteStartObject("attrs");
                            writer.WriteString("color", color);
                            writer.WriteEndObject();
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                break;
        }

        if (node.Content.Count > 0)
        {
            writer.WriteStartArray("content");
            foreach (var child in node.Content)
            {
                Write(writer, child);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>Walks the editor's JSON, keeping only what the allowlist names; the first problem stops it.</summary>
    private sealed class Reader(string field)
    {
        public DomainError? Error { get; private set; }

        public FormattedNode? Read(JsonElement element, string? parent, bool inTable)
        {
            if (Error is not null)
            {
                return null;
            }

            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                return Fail();
            }

            var type = typeElement.GetString()!;
            if (!Allowed(type, parent))
            {
                return Fail();
            }

            var attrs = element.TryGetProperty("attrs", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
            var node = new FormattedNode(type);
            switch (type)
            {
                case "text":
                    if (!element.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                    {
                        return Fail();
                    }

                    return node with { Text = text.GetString(), Marks = parent == "codeBlock" ? [] : ReadMarks(element) };
                case "hardBreak":
                    return node;
                case "heading":
                    var level = Int(attrs, "level") ?? 1;
                    if (level is < 1 or > 6)
                    {
                        return Fail();
                    }

                    node = node with { Level = level };
                    break;
                case "paragraph":
                    node = node with { Size = String(attrs, "size") == "small" ? "small" : null };
                    break;
                case "orderedList":
                    node = node with
                    {
                        Start = Int(attrs, "start") is { } start and >= 1 and <= 9999 ? start : 1,
                        ListStyle = OneOf(String(attrs, "type"), NumberStyles),
                    };
                    break;
                case "bulletList":
                    node = node with { ListStyle = OneOf(String(attrs, "listStyle"), BulletStyles) };
                    break;
                case "taskItem":
                    node = node with { Checked = attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty("checked", out var done) && done.ValueKind == JsonValueKind.True };
                    break;
                case "tableCell" or "tableHeader":
                    var colspan = Int(attrs, "colspan") ?? 1;
                    var rowspan = Int(attrs, "rowspan") ?? 1;
                    if (colspan is < 1 or > MaxTableColumns || rowspan is < 1 or > MaxTableRows)
                    {
                        return Fail();
                    }

                    var widths = Widths(attrs, colspan);
                    node = node with
                    {
                        Colspan = colspan == 1 ? null : colspan,
                        Rowspan = rowspan == 1 ? null : rowspan,
                        ColumnWidths = widths,
                        Background = OneOf(String(attrs, "background"), Colors),
                    };
                    break;
                case "mention":
                    return Guid.TryParse(String(attrs, "id"), out var id)
                        ? node with { MentionId = id, Label = Cut(String(attrs, "label")?.Trim() ?? "", LabelMaxLength) }
                        : Fail();
                case "image":
                    return Long(attrs, "attachmentId") is { } attachmentId and > 0
                        ? node with { AttachmentId = attachmentId, Alt = String(attrs, "alt") is { } alt ? Cut(alt, AltMaxLength) : null }
                        : Fail();
                case "table":
                    if (inTable)
                    {
                        return Fail();
                    }

                    inTable = true;
                    break;
            }

            var content = new List<FormattedNode>();
            if (element.TryGetProperty("content", out var children))
            {
                if (children.ValueKind != JsonValueKind.Array)
                {
                    return Fail();
                }

                foreach (var child in children.EnumerateArray())
                {
                    if (Read(child, type, inTable) is not { } kept)
                    {
                        return null;
                    }

                    content.Add(kept);
                }
            }

            if (type == "table" && (content.Count is 0 or > MaxTableRows
                || content.Any(row => row.Content.Count is 0 or > MaxTableColumns)))
            {
                return Fail();
            }

            return node with { Content = content };
        }

        private static bool Allowed(string type, string? parent) => type switch
        {
            "doc" => parent is null,
            "listItem" => parent is "bulletList" or "orderedList",
            "taskItem" => parent == "taskList",
            "tableRow" => parent == "table",
            "tableCell" or "tableHeader" => parent == "tableRow",
            "text" => parent is "paragraph" or "heading" or "codeBlock",
            "hardBreak" or "mention" => parent is "paragraph" or "heading",
            _ when BlockTypes.Contains(type) => parent is not null && BlockParents.Contains(parent),
            _ => false,
        };

        private List<FormattedMark> ReadMarks(JsonElement element)
        {
            if (!element.TryGetProperty("marks", out var marks) || marks.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var kept = new List<FormattedMark>();
            foreach (var mark in marks.EnumerateArray())
            {
                var type = mark.ValueKind == JsonValueKind.Object && mark.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()!
                    : "";
                if (!MarkTypes.Contains(type))
                {
                    Fail();
                    return [];
                }

                if (kept.Any(m => m.Type == type))
                {
                    continue;
                }

                if (type == "link")
                {
                    var attrs = mark.TryGetProperty("attrs", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
                    if (SafeLink(String(attrs, "href")) is not { } href)
                    {
                        Error ??= new DomainError(InvalidLinkCode, "Links must start with http, https or mailto.", field);
                        return [];
                    }

                    kept.Add(new FormattedMark(type, href));
                }
                else if (type == "textColor")
                {
                    var attrs = mark.TryGetProperty("attrs", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
                    if (OneOf(String(attrs, "color"), Colors) is not { } color)
                    {
                        Fail();
                        return [];
                    }

                    kept.Add(new FormattedMark(type, Color: color));
                }
                else
                {
                    kept.Add(new FormattedMark(type));
                }
            }

            return kept;
        }

        private static string? SafeLink(string? href)
        {
            var trimmed = href?.Trim();
            return trimmed is { Length: > 0 and <= LinkMaxLength }
                && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
                ? trimmed
                : null;
        }

        private static string? String(JsonElement attrs, string name) =>
            attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static int? Int(JsonElement attrs, string name) =>
            attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : null;

        private static long? Long(JsonElement attrs, string name) =>
            attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number
                : null;

        private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

        /// <summary>The value when it is one of the allowed names; anything else is dropped.</summary>
        private static string? OneOf(string? value, IReadOnlyList<string> allowed) =>
            value is not null && allowed.Contains(value, StringComparer.Ordinal) ? value : null;

        /// <summary>A resized cell's column widths in pixels, one per column it spans; anything else is dropped.</summary>
        private static List<int>? Widths(JsonElement attrs, int colspan)
        {
            if (attrs.ValueKind != JsonValueKind.Object || !attrs.TryGetProperty("colwidth", out var value)
                || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != colspan)
            {
                return null;
            }

            var widths = new List<int>();
            foreach (var width in value.EnumerateArray())
            {
                if (width.ValueKind != JsonValueKind.Number || !width.TryGetInt32(out var pixels) || pixels is < 1 or > MaxColumnWidth)
                {
                    return null;
                }

                widths.Add(pixels);
            }

            return widths;
        }

        private FormattedNode? Fail()
        {
            Error ??= Unsupported(field);
            return null;
        }
    }
}

/// <summary>One node of a checked document. Only the attributes the allowlist keeps exist here.</summary>
public sealed record FormattedNode(string Type)
{
    public IReadOnlyList<FormattedNode> Content { get; init; } = [];

    /// <summary>For text nodes.</summary>
    public string? Text { get; init; }

    public IReadOnlyList<FormattedMark> Marks { get; init; } = [];

    /// <summary>A heading's level, 1 to 6.</summary>
    public int? Level { get; init; }

    /// <summary>A numbered list's first number.</summary>
    public int? Start { get; init; }

    /// <summary>"small" for Jira's small text; null for normal text.</summary>
    public string? Size { get; init; }

    /// <summary>A bulleted list's bullets (<see cref="FormattedText.BulletStyles"/>) or a numbered list's numbering
    /// (<see cref="FormattedText.NumberStyles"/>); null for discs and 1, 2, 3.</summary>
    public string? ListStyle { get; init; }

    /// <summary>A checklist item that is ticked.</summary>
    public bool? Checked { get; init; }

    /// <summary>A table cell spanning several columns or rows.</summary>
    public int? Colspan { get; init; }

    public int? Rowspan { get; init; }

    /// <summary>A resized cell's column widths in pixels.</summary>
    public IReadOnlyList<int>? ColumnWidths { get; init; }

    /// <summary>A table cell's colour (<see cref="FormattedText.Colors"/>).</summary>
    public string? Background { get; init; }

    public Guid? MentionId { get; init; }

    /// <summary>A mention's name when it was written, shown when the person is unknown.</summary>
    public string? Label { get; init; }

    public long? AttachmentId { get; init; }

    public string? Alt { get; init; }
}

/// <summary>A mark on text: bold, italic, underline, strike, code, subscript, superscript, a colour by name, or a link with
/// its checked address.</summary>
public sealed record FormattedMark(string Type, string? Href = null, string? Color = null);
