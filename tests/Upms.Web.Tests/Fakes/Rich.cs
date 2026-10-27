using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>Descriptions and comments as the services return them (Phase 4 research R4).</summary>
public static class Rich
{
    /// <summary>Phase 1–3 plain text.</summary>
    public static RichText Plain(string text) => new(TextFormat.Plain, text, text);

    /// <summary>The document an editor sends, as the service keeps it; null for anything it would refuse.</summary>
    public static RichText? FromDocument(string? document) =>
        document is not null && FormattedText.Parse(document, "Body").Value is { } text
            ? new RichText(TextFormat.Document, text.Json, text.PlainText)
            : null;

    /// <summary>Plain text as the editor's document: one paragraph per line.</summary>
    public static string Document(string text) => FormattedText.FromPlainText(text).Json;
}
