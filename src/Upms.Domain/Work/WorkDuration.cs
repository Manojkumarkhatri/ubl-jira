using System.Globalization;
using System.Text.RegularExpressions;

namespace Upms.Domain.Work;

/// <summary>Time as Jira writes it: "2w 3d 4h 30m", where a day is 8 hours and a week 5 days. A number on its own is
/// minutes, as in Jira.</summary>
public static partial class WorkDuration
{
    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 8;
    public const int DaysPerWeek = 5;
    public const int MinutesPerDay = MinutesPerHour * HoursPerDay;
    public const int MinutesPerWeek = MinutesPerDay * DaysPerWeek;

    /// <summary>999 weeks: more than any one item takes.</summary>
    public const int MaxMinutes = 999 * MinutesPerWeek;

    public const string InvalidCode = "InvalidDuration";

    /// <summary>Reads "2w 3d 4h 30m", "1.5h", "90m" or "45"; false for anything else, zero, or more than <see cref="MaxMinutes"/>.</summary>
    public static bool TryParse(string? text, out int minutes)
    {
        minutes = 0;
        var trimmed = (text ?? "").Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (decimal.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var bare))
        {
            return Within(bare, out minutes);
        }

        var parts = Part().Matches(trimmed);
        if (parts.Count == 0 || string.Concat(parts.Select(p => p.Value)).Replace(" ", "", StringComparison.Ordinal) != trimmed.Replace(" ", "", StringComparison.Ordinal))
        {
            return false;
        }

        decimal total = 0;
        foreach (Match part in parts)
        {
            var amount = decimal.Parse(part.Groups["n"].Value, CultureInfo.InvariantCulture);
            total += amount * part.Groups["u"].Value switch
            {
                "w" => MinutesPerWeek,
                "d" => MinutesPerDay,
                "h" => MinutesPerHour,
                _ => 1,
            };
        }

        return Within(total, out minutes);
    }

    /// <summary>"1w 2d 3h 30m"; "0m" for nothing.</summary>
    public static string Format(int minutes)
    {
        if (minutes <= 0)
        {
            return "0m";
        }

        var parts = new List<string>();
        var weeks = minutes / MinutesPerWeek;
        minutes %= MinutesPerWeek;
        var days = minutes / MinutesPerDay;
        minutes %= MinutesPerDay;
        var hours = minutes / MinutesPerHour;
        minutes %= MinutesPerHour;
        if (weeks > 0)
        {
            parts.Add($"{weeks}w");
        }

        if (days > 0)
        {
            parts.Add($"{days}d");
        }

        if (hours > 0)
        {
            parts.Add($"{hours}h");
        }

        if (minutes > 0)
        {
            parts.Add($"{minutes}m");
        }

        return string.Join(' ', parts);
    }

    public static string? FormatOrNull(int? minutes) => minutes is { } m ? Format(m) : null;

    private static bool Within(decimal value, out int minutes)
    {
        minutes = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return value > 0 && value <= MaxMinutes && minutes > 0;
    }

    [GeneratedRegex(@"(?<n>\d+(\.\d+)?)\s*(?<u>[wdhm])", RegexOptions.CultureInvariant)]
    private static partial Regex Part();
}
