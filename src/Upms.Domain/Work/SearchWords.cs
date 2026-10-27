using System.Text;
using System.Text.RegularExpressions;

namespace Upms.Domain.Work;

/// <summary>How text becomes search words (Phase 4 research R15): lower case (invariant culture), split on anything that
/// is not a letter or digit, 2 to 40 characters each, each once. A search matches words that start with what was typed.</summary>
public static partial class SearchWords
{
    public const int MinLength = 2;
    public const int MaxLength = 40;
    public const int MaxQueryWords = 10;

    /// <summary>The distinct words of a text, in order of first appearance.</summary>
    public static IReadOnlyList<string> Of(string? text)
    {
        var words = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var c in (text ?? "").Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(char.ToLowerInvariant(c));
                continue;
            }

            Keep(word, words, seen);
        }

        Keep(word, words, seen);
        return words;
    }

    /// <summary>The words of what was typed in the search, at most 10.</summary>
    public static IReadOnlyList<string> Query(string? text) => Of(text).Take(MaxQueryWords).ToList();

    /// <summary>Text that looks like a work item key, such as "mob-12", and the key in upper case (research R16).</summary>
    public static bool LooksLikeKey(string? text, out string key)
    {
        var trimmed = (text ?? "").Trim();
        key = KeyPattern().IsMatch(trimmed) ? trimmed.ToUpperInvariant() : "";
        return key.Length > 0;
    }

    private static void Keep(StringBuilder word, List<string> words, HashSet<string> seen)
    {
        if (word.Length >= MinLength)
        {
            var kept = word.Length > MaxLength ? word.ToString(0, MaxLength) : word.ToString();
            if (seen.Add(kept))
            {
                words.Add(kept);
            }
        }

        word.Clear();
    }

    [GeneratedRegex(@"^[A-Za-z0-9]{2,10}-\d{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
