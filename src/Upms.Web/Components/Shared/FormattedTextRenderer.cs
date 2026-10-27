using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Upms.Domain.Work;

namespace Upms.Web.Components.Shared;

/// <summary>Where an image in formatted text stands (Phase 4 FR-023).</summary>
public enum ImageState
{
    /// <summary>Removed, blocked or unknown: never loaded.</summary>
    Missing,

    /// <summary>Waiting for its malware check.</summary>
    Checking,

    /// <summary>Checked: shown from its preview address.</summary>
    Ready,
}

/// <summary>Turns a checked formatted-text document into HTML on the server (Phase 4 FR-003, research R3). Only the
/// allowlisted nodes and marks exist in the document; each becomes a fixed element, every text node and attribute value is
/// encoded, links can only be http, https or mailto, and images only load from this site's preview address once their
/// malware check has passed, so nothing a user writes can run.</summary>
public static class FormattedTextRenderer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    /// <param name="images">Each image's state from the item's attachments; without it no image is loaded.</param>
    public static string ToHtml(FormattedText text, Func<long, ImageState>? images = null)
    {
        var writer = new Writer(images ?? (_ => ImageState.Missing));
        foreach (var block in text.Root.Content)
        {
            writer.Block(block);
        }

        return writer.Html.ToString();
    }

    private sealed class Writer(Func<long, ImageState> images)
    {
        public StringBuilder Html { get; } = new();

        public void Block(FormattedNode node)
        {
            switch (node.Type)
            {
                case "paragraph":
                    Html.Append(node.Size == "small" ? "<p class=\"ft-small\">" : "<p>");
                    Inline(node);
                    Html.Append("</p>");
                    break;
                case "heading":
                    // Below the drawer's own section headings: level 1 is an h4, and the levels below h6 keep their size by class
                    // (WCAG 1.3.1).
                    var level = Math.Clamp(node.Level ?? 1, 1, 6);
                    var element = $"h{Math.Min(level + 3, 6)}";
                    Html.Append('<').Append(element).Append(" class=\"ft-h").Append(level.ToString(CultureInfo.InvariantCulture)).Append("\">");
                    Inline(node);
                    Html.Append("</").Append(element).Append('>');
                    break;
                case "bulletList":
                    Container(node.ListStyle is { } bullets ? $"<ul class=\"ft-list-{bullets}\">" : "<ul>", "</ul>", node);
                    break;
                case "orderedList":
                    Container($"<ol{(node.Start is { } start and not 1 ? $" start=\"{start.ToString(CultureInfo.InvariantCulture)}\"" : "")}{(node.ListStyle is { } numbering ? $" type=\"{numbering}\"" : "")}>",
                        "</ol>", node);
                    break;
                case "listItem":
                    Container("<li>", "</li>", node);
                    break;
                case "taskList":
                    Container("<ul class=\"ft-tasks\">", "</ul>", node);
                    break;
                case "taskItem":
                    // A ticked item reads as done; the box is a picture, the words say it (WCAG 1.1.1).
                    Html.Append(node.Checked == true ? "<li class=\"ft-task is-done\">" : "<li class=\"ft-task\">")
                        .Append("<span class=\"ft-check\" aria-hidden=\"true\"></span><span class=\"sr-only\">")
                        .Append(node.Checked == true ? "Done: " : "To do: ").Append("</span><div>");
                    foreach (var child in node.Content)
                    {
                        Block(child);
                    }

                    Html.Append("</div></li>");
                    break;
                case "blockquote":
                    Container("<blockquote>", "</blockquote>", node);
                    break;
                case "codeBlock":
                    Html.Append("<pre><code>");
                    foreach (var text in node.Content)
                    {
                        Html.Append(Encoder.Encode(text.Text ?? ""));
                    }

                    Html.Append("</code></pre>");
                    break;
                case "table":
                    Table(node);
                    break;
                case "image":
                    Image(node);
                    break;
            }
        }

        private void Container(string open, string close, FormattedNode node)
        {
            Html.Append(open);
            foreach (var child in node.Content)
            {
                Block(child);
            }

            Html.Append(close);
        }

        /// <summary>A header row becomes the table's head with column headers (FR-005); the table scrolls on its own on
        /// small screens.</summary>
        private void Table(FormattedNode table)
        {
            Html.Append("<div class=\"ft-table\"><table>");
            var rows = table.Content;
            var hasHead = rows.Count > 0 && rows[0].Content.All(c => c.Type == "tableHeader");
            if (hasHead)
            {
                Html.Append("<thead>");
                Row(rows[0], "col");
                Html.Append("</thead>");
            }

            Html.Append("<tbody>");
            foreach (var row in rows.Skip(hasHead ? 1 : 0))
            {
                Row(row, "row");
            }

            Html.Append("</tbody></table></div>");
        }

        private void Row(FormattedNode row, string headerScope)
        {
            Html.Append("<tr>");
            foreach (var cell in row.Content)
            {
                var header = cell.Type == "tableHeader";
                // Spans and colours come from the checked document's fixed values, never from text a user typed.
                var extra = new StringBuilder();
                if (cell.Colspan is { } columns)
                {
                    extra.Append(" colspan=\"").Append(columns.ToString(CultureInfo.InvariantCulture)).Append('"');
                }

                if (cell.Rowspan is { } rows)
                {
                    extra.Append(" rowspan=\"").Append(rows.ToString(CultureInfo.InvariantCulture)).Append('"');
                }

                if (cell.Background is { } background)
                {
                    extra.Append(" class=\"ft-bg-").Append(background).Append('"');
                }

                Container(header ? $"<th scope=\"{headerScope}\"{extra}>" : $"<td{extra}>", header ? "</th>" : "</td>", cell);
            }

            Html.Append("</tr>");
        }

        /// <summary>Only from this site's preview address, and only once checked (FR-025): the address is built here
        /// from the attachment's id, never taken from the document.</summary>
        private void Image(FormattedNode node)
        {
            var id = node.AttachmentId!.Value;
            var alt = node.Alt ?? "";
            switch (images(id))
            {
                case ImageState.Ready:
                    Html.Append("<p class=\"ft-image\"><img src=\"attachments/").Append(id.ToString(CultureInfo.InvariantCulture))
                        .Append("/preview\" alt=\"").Append(Encoder.Encode(alt)).Append("\" loading=\"lazy\"></p>");
                    break;
                case ImageState.Checking:
                    Html.Append("<p class=\"ft-image-note\">Image “").Append(Encoder.Encode(alt)).Append("” is being checked for malware.</p>");
                    break;
                default:
                    Html.Append("<p class=\"ft-image-note\">Image removed.</p>");
                    break;
            }
        }

        private void Inline(FormattedNode block)
        {
            if (block.Content.Count == 0)
            {
                Html.Append("<br>"); // an empty paragraph keeps its line
                return;
            }

            foreach (var node in block.Content)
            {
                switch (node.Type)
                {
                    case "text":
                        Text(node);
                        break;
                    case "hardBreak":
                        Html.Append("<br>");
                        break;
                    case "mention":
                        Html.Append("<span class=\"mention\">@").Append(Encoder.Encode(node.Label ?? "")).Append("</span>");
                        break;
                }
            }
        }

        private void Text(FormattedNode node)
        {
            var close = new Stack<string>();
            foreach (var mark in node.Marks)
            {
                var (open, end) = mark.Type switch
                {
                    "bold" => ("<strong>", "</strong>"),
                    "italic" => ("<em>", "</em>"),
                    "strike" => ("<s>", "</s>"),
                    "underline" => ("<u>", "</u>"),
                    "subscript" => ("<sub>", "</sub>"),
                    "superscript" => ("<sup>", "</sup>"),
                    "textColor" when mark.Color is { } color => ($"<span class=\"ft-color-{color}\">", "</span>"),
                    "code" => ("<code>", "</code>"),
                    "link" when mark.Href is { } href =>
                        ($"<a href=\"{Encoder.Encode(href)}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">", "</a>"),
                    _ => ("", ""),
                };
                Html.Append(open);
                close.Push(end);
            }

            Html.Append(Encoder.Encode(node.Text ?? ""));
            while (close.TryPop(out var end))
            {
                Html.Append(end);
            }
        }
    }
}
