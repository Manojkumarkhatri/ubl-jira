using Upms.Application.Work;

namespace Upms.Web.Components.Pages.Board;

/// <summary>Columns show their cards 50 at a time, so a board with thousands of cards stays quick; "Show more" adds the
/// next 50. The card last moved or created is always shown, wherever it landed, so a move never seems to lose it.</summary>
public static class BoardPaging
{
    public const int PageSize = 50;

    /// <summary>Which cards to show: the first <paramref name="shown"/>, and the revealed card. When the revealed card is
    /// past them it shows after "Show more", so a long column never has to draw everything before it; "Show more" brings
    /// it back to its place.</summary>
    public static CardWindow Window(IReadOnlyList<CardView> cards, int shown, string? revealKey)
    {
        shown = Math.Min(shown, cards.Count);
        var index = revealKey is null ? -1 : IndexOf(cards, revealKey, shown);
        return index >= 0
            ? new CardWindow(shown, cards.Count - shown - 1, cards[index])
            : new CardWindow(shown, cards.Count - shown, null);
    }

    /// <summary>"Show 50 more", or fewer when fewer are left.</summary>
    public static string MoreText(int hidden) =>
        string.Create(System.Globalization.CultureInfo.CurrentCulture, $"Show {Math.Min(PageSize, hidden):N0} more");

    /// <summary>"1,234 not shown".</summary>
    public static string HiddenText(int hidden) =>
        string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{hidden:N0} not shown");

    private static int IndexOf(IReadOnlyList<CardView> cards, string key, int from)
    {
        for (var i = from; i < cards.Count; i++)
        {
            if (cards[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <param name="Shown">The first cards shown, in order.</param>
/// <param name="Hidden">The cards behind "Show more".</param>
/// <param name="Tail">The card just moved or created, when it is past the shown ones: it shows after "Show more".</param>
public readonly record struct CardWindow(int Shown, int Hidden, CardView? Tail);
