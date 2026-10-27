namespace Upms.Domain.Work;

/// <summary>How a description or comment is stored (Phase 4 research R4): plain text from Phases 1–3, or a checked
/// <see cref="FormattedText"/> document.</summary>
public enum TextFormat : byte
{
    Plain = 0,
    Document = 1,
}
