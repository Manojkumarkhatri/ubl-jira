using System.Globalization;
using System.Text;

namespace Upms.Domain.Projects;

/// <summary>Suggests a project key from its name (FR-011): initials of several words ("Website Revamp"
/// → WR), or the first letters of a single word ("Marketing" → MAR). A key in use gets a numeric suffix.</summary>
public static class ProjectKeySuggester
{
    private const int SingleWordLength = 3;

    public static string Suggest(string name, Func<string, bool>? isTaken = null)
    {
        // Numbers are noise in initials ("HR-2026 onboarding" → HO) and a key cannot start with one.
        var words = Words(name).Select(TrimLeadingDigits).Where(w => w.Length > 0).ToList();
        if (words.Count == 0)
        {
            return "";
        }

        var candidate = words.Count >= 2
            ? string.Concat(words.Select(w => w[0]))
            : words[0][..Math.Min(SingleWordLength, words[0].Length)];
        if (candidate.Length > Project.KeyMaxLength)
        {
            candidate = candidate[..Project.KeyMaxLength];
        }

        if (!Project.IsValidKey(candidate))
        {
            return "";
        }

        return isTaken is null ? candidate : FirstFree(candidate, isTaken);
    }

    private static string FirstFree(string candidate, Func<string, bool> isTaken)
    {
        if (!isTaken(candidate))
        {
            return candidate;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var number = suffix.ToString(CultureInfo.InvariantCulture);
            var stem = candidate.Length + number.Length > Project.KeyMaxLength
                ? candidate[..(Project.KeyMaxLength - number.Length)]
                : candidate;
            var key = stem + number;
            if (!isTaken(key))
            {
                return key;
            }
        }

        return "";
    }

    /// <summary>Upper-case ASCII words: accents removed, anything else splits words.</summary>
    private static List<string> Words(string name)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in (name ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                current.Append(char.ToUpperInvariant(c));
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    private static string TrimLeadingDigits(string value)
    {
        var start = 0;
        while (start < value.Length && char.IsAsciiDigit(value[start]))
        {
            start++;
        }

        return value[start..];
    }
}
