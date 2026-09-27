using System.Text.RegularExpressions;

namespace Upms.Web.Components.Shared;

/// <summary>Splits plain text into text runs and web links (research R10). Only absolute <c>http</c> and
/// <c>https</c> addresses become links; trailing punctuation stays text, so "see https://x.org." links
/// "https://x.org".</summary>
public static partial class PlainTextParts
{
    public readonly record struct Part(string Text, bool IsLink);

    public static IEnumerable<Part> Split(string text)
    {
        var position = 0;
        foreach (Match match in Candidates().Matches(text))
        {
            var link = TrimTrailing(match.Value);
            if (!IsWebAddress(link))
            {
                continue;
            }

            if (match.Index > position)
            {
                yield return new Part(text[position..match.Index], false);
            }

            yield return new Part(link, true);
            position = match.Index + link.Length;
        }

        if (position < text.Length)
        {
            yield return new Part(text[position..], false);
        }
    }

    private static bool IsWebAddress(string candidate) =>
        Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    // Sentence punctuation after an address, and a closing bracket without an opening one, are not part of it.
    private static string TrimTrailing(string candidate)
    {
        var end = candidate.Length;
        while (end > 0)
        {
            var last = candidate[end - 1];
            if (".,;:!?'\"".Contains(last, StringComparison.Ordinal))
            {
                end--;
            }
            else if (last == ')' && candidate.AsSpan(0, end).Count('(') < candidate.AsSpan(0, end).Count(')'))
            {
                end--;
            }
            else
            {
                break;
            }
        }

        return candidate[..end];
    }

    [GeneratedRegex(@"\bhttps?://[^\s<>""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Candidates();
}
