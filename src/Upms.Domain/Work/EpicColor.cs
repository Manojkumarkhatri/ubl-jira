namespace Upms.Domain.Work;

/// <summary>An epic's color, from a fixed palette whose light and dark tags all meet 4.5:1 contrast (Jira parity pass
/// FR-025; research R15). Stored as its number, 0–7.</summary>
public enum EpicColor : byte
{
    Purple,
    Blue,
    Teal,
    Green,
    Yellow,
    Orange,
    Red,
    Gray,
}

public static class EpicColors
{
    public const int Count = 8;

    /// <summary>The color a new epic takes, in turn by its number, as Jira gives epics their colors.</summary>
    public static EpicColor ForNumber(int number) => (EpicColor)((number - 1) % Count);
}
