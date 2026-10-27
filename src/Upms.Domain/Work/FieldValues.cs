using System.Globalization;
using System.Text.Json;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>How a space field's value is checked, stored and shown. Single values are stored as text (a date as
/// <c>yyyy-MM-dd</c>, a number in invariant form, a time stamp in round-trip form, a dropdown as its option id); lists
/// (labels, checkboxes, people) as a JSON array.</summary>
public static class FieldValues
{
    public const int ShortTextMaxLength = 255;
    public const int ParagraphMaxLength = 10_000;
    public const int UrlMaxLength = 2_000;
    public const int MaxItems = 20;
    public const decimal MaxNumber = 999_999_999_999m;

    /// <summary>The value to store, or null to clear it.</summary>
    public static DomainResult<string?> Normalize(SpaceField field, string? value, IReadOnlyList<string>? items)
    {
        var text = value?.Trim() ?? "";
        switch (field.Type)
        {
            case FieldType.Formula:
                return DomainError.Rule("FormulaReadOnly", "A formula field works out its own value.");
            case FieldType.ShortText:
                return text.Length > ShortTextMaxLength
                    ? DomainError.Invalid("Value", $"Use at most {ShortTextMaxLength} characters.")
                    : Ok(text.Length == 0 ? null : text);
            case FieldType.Paragraph:
                var paragraph = (value ?? "").TrimEnd();
                return paragraph.Length > ParagraphMaxLength
                    ? DomainError.Invalid("Value", $"Use at most {ParagraphMaxLength:N0} characters.")
                    : Ok(paragraph.Trim().Length == 0 ? null : paragraph);
            case FieldType.Date:
                if (text.Length == 0)
                {
                    return Ok(null);
                }

                return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && date >= WorkItem.EarliestDate && date <= WorkItem.LatestDate
                    ? Ok(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    : DomainError.Invalid("Value", "Enter a date between 2000 and 2099.");
            case FieldType.Number:
                if (text.Length == 0)
                {
                    return Ok(null);
                }

                return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && Math.Abs(number) <= MaxNumber
                    ? Ok(FormatNumber(Math.Round(number, 4, MidpointRounding.AwayFromZero)))
                    : DomainError.Invalid("Value", "Enter a number, such as 12 or 3.5.");
            case FieldType.Timestamp:
                if (text.Length == 0)
                {
                    return Ok(null);
                }

                return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
                    && stamp.Year is >= 2000 and <= 2099
                    ? Ok(stamp.ToString("o", CultureInfo.InvariantCulture))
                    : DomainError.Invalid("Value", "Enter a date and time between 2000 and 2099.");
            case FieldType.Url:
                if (text.Length == 0)
                {
                    return Ok(null);
                }

                return text.Length <= UrlMaxLength && Uri.TryCreate(text, UriKind.Absolute, out var url)
                    && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
                    ? Ok(url.ToString())
                    : DomainError.Invalid("Value", "Enter a web address starting with https:// or http://.");
            case FieldType.Dropdown:
                if (text.Length == 0)
                {
                    return Ok(null);
                }

                return field.Options.Any(o => o.Id == text) ? Ok(text) : DomainError.Invalid("Value", "Choose one of the options.");
            case FieldType.Checkbox:
                var chosen = (items ?? []).ToHashSet(StringComparer.Ordinal);
                if (chosen.Any(id => field.Options.All(o => o.Id != id)))
                {
                    return DomainError.Invalid("Value", "Choose from the options.");
                }

                // Kept in the options' order, so the same choice is always stored the same way.
                return Ok(List(field.Options.Where(o => chosen.Contains(o.Id)).Select(o => o.Id).ToList()));
            case FieldType.Labels:
                var labels = (items ?? []).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (labels.Count > MaxItems)
                {
                    return DomainError.Invalid("Value", $"Use at most {MaxItems} labels.");
                }

                foreach (var label in labels)
                {
                    if (Label.Check(label) is { } invalid)
                    {
                        return DomainError.Invalid("Value", invalid.Message);
                    }
                }

                return Ok(List(labels));
            default: // People
                var people = new List<string>();
                foreach (var item in items ?? [])
                {
                    if (!Guid.TryParse(item, out var id))
                    {
                        return DomainError.Invalid("Value", "Choose people of the space.");
                    }

                    if (!people.Contains(id.ToString()))
                    {
                        people.Add(id.ToString());
                    }
                }

                return people.Count > MaxItems ? DomainError.Invalid("Value", $"Choose at most {MaxItems} people.") : Ok(List(people));
        }
    }

    /// <summary>The items of a list value (labels, checkbox option ids, people ids).</summary>
    public static IReadOnlyList<string> Items(string? stored) =>
        stored is { Length: > 0 } && stored[0] == '[' ? JsonSerializer.Deserialize<List<string>>(stored) ?? [] : [];

    /// <summary>The value as words, for history and exports: option names, people's names, a readable date.</summary>
    public static string? Display(SpaceField field, string? stored, Func<Guid, string> nameOf)
    {
        if (stored is null)
        {
            return null;
        }

        return field.Type switch
        {
            FieldType.Date => DateOnly.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : stored,
            FieldType.Timestamp => DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)
                ? stamp.UtcDateTime.ToString("d MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture) : stored,
            FieldType.Dropdown => field.Options.FirstOrDefault(o => o.Id == stored)?.Name ?? "(removed option)",
            FieldType.Checkbox => string.Join(", ", Items(stored).Select(id => field.Options.FirstOrDefault(o => o.Id == id)?.Name ?? "(removed option)")),
            FieldType.Labels => string.Join(", ", Items(stored)),
            FieldType.People => string.Join(", ", Items(stored).Select(id => Guid.TryParse(id, out var person) ? nameOf(person) : id)),
            _ => stored,
        };
    }

    public static string FormatNumber(decimal number) => number.ToString("0.####", CultureInfo.InvariantCulture);

    private static string? List(List<string> items) => items.Count == 0 ? null : JsonSerializer.Serialize(items);

    private static DomainResult<string?> Ok(string? value) => DomainResult<string?>.Ok(value);
}
