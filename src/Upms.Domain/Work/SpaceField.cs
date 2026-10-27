using System.Text.Json;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>The kinds of field a Space admin can create, as Jira's "Create a field".</summary>
public enum FieldType
{
    ShortText,
    Paragraph,
    Date,
    Number,

    /// <summary>Worked out from number fields; never typed in.</summary>
    Formula,

    /// <summary>A date and a time.</summary>
    Timestamp,
    Labels,
    Dropdown,
    Checkbox,
    People,
    Url,
}

/// <summary>One choice of a dropdown or checkbox field. The id stays when the name changes, so values keep pointing at it.</summary>
public sealed record FieldOption(string Id, string Name);

/// <summary>A field a Space admin created for the space's work items (Jira's custom fields in a team-managed space). Its
/// place on each work type is set in that type's <see cref="WorkTypeLayout"/>. A deleted field disappears from screens;
/// its values stay, so history still reads.</summary>
public sealed class SpaceField
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;
    public const int OptionMaxLength = 60;
    public const int MaxOptions = 100;
    public const int FormulaMaxLength = 500;
    public const string NameTakenCode = "FieldNameTaken";
    public const string OptionsCode = "InvalidOptions";

    private SpaceField()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public string Name { get; private set; } = "";

    public FieldType Type { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The choices of a dropdown or checkbox field, as JSON.</summary>
    public string? OptionsJson { get; private set; }

    /// <summary>A formula field's expression, such as <c>{Story point estimate} * 2</c>.</summary>
    public string? Formula { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<FieldOption> Options =>
        OptionsJson is null ? [] : JsonSerializer.Deserialize<List<FieldOption>>(OptionsJson) ?? [];

    public bool HasOptions => Type is FieldType.Dropdown or FieldType.Checkbox;

    /// <summary>A new field. Dropdown and checkbox fields need at least one choice; a formula needs its expression, checked
    /// by the caller against the space's number fields.</summary>
    public static DomainResult<SpaceField> Create(long projectId, string name, FieldType type, string? description,
        IReadOnlyList<(string? Id, string Name)>? options, string? formula, Guid actorId, DateTimeOffset now)
    {
        var field = new SpaceField { ProjectId = projectId, Type = type, CreatedById = actorId, CreatedAt = now };
        return field.Change(name, description, options, formula, now) is { } error ? error : field;
    }

    public DomainError? Change(string name, string? description, IReadOnlyList<(string? Id, string Name)>? options, string? formula,
        DateTimeOffset now)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return DomainError.Invalid("Name", "Enter a name for the field.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            return DomainError.Invalid("Name", $"A field name can have at most {NameMaxLength} characters.");
        }

        if (trimmed.Contains('{') || trimmed.Contains('}'))
        {
            return DomainError.Invalid("Name", "A field name cannot contain { or }.");
        }

        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text is { Length: > DescriptionMaxLength })
        {
            return DomainError.Invalid("Description", $"The description can have at most {DescriptionMaxLength} characters.");
        }

        string? optionsJson = null;
        if (HasOptions)
        {
            var chosen = (options ?? []).Select(o => (o.Id, Name: o.Name.Trim())).Where(o => o.Name.Length > 0).ToList();
            if (chosen.Count == 0)
            {
                return DomainError.Invalid("Options", "Add at least one option.");
            }

            if (chosen.Count > MaxOptions || chosen.Any(o => o.Name.Length > OptionMaxLength))
            {
                return DomainError.Invalid("Options", $"Use at most {MaxOptions} options of {OptionMaxLength} characters each.");
            }

            if (chosen.Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != chosen.Count)
            {
                return DomainError.Invalid("Options", "Each option needs a different name.");
            }

            // Kept options keep their id, so values that chose them still do; new ones get the next number.
            var known = Options.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            var next = Options.Select(o => int.TryParse(o.Id, out var n) ? n : 0).DefaultIfEmpty(0).Max();
            var saved = chosen.Select(o => new FieldOption(o.Id is { } id && known.Contains(id) ? id : (++next).ToString(System.Globalization.CultureInfo.InvariantCulture), o.Name)).ToList();
            optionsJson = JsonSerializer.Serialize(saved);
        }

        string? expression = null;
        if (Type == FieldType.Formula)
        {
            expression = formula?.Trim();
            if (string.IsNullOrEmpty(expression))
            {
                return DomainError.Invalid("Formula", "Enter a formula, such as {Story point estimate} * 2.");
            }

            if (expression.Length > FormulaMaxLength)
            {
                return DomainError.Invalid("Formula", $"A formula can have at most {FormulaMaxLength} characters.");
            }
        }

        Name = trimmed;
        Description = text;
        OptionsJson = optionsJson;
        Formula = expression;
        UpdatedAt = now;
        return null;
    }

    /// <summary>Points a formula at a renamed field.</summary>
    public void RenameReference(string oldName, string newName, DateTimeOffset now)
    {
        if (Formula is null)
        {
            return;
        }

        var renamed = FormulaExpression.RenameReference(Formula, oldName, newName);
        if (!string.Equals(renamed, Formula, StringComparison.Ordinal))
        {
            Formula = renamed;
            UpdatedAt = now;
        }
    }

    public void Delete(DateTimeOffset now)
    {
        IsDeleted = true;
        DeletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>"Short text", "Time stamp", as the field type column and the "Create a field" tiles name them.</summary>
    public static string TypeName(FieldType type) => type switch
    {
        FieldType.ShortText => "Short text",
        FieldType.Timestamp => "Time stamp",
        FieldType.Url => "URL",
        _ => type.ToString(),
    };
}

/// <summary>A work item's value for one <see cref="SpaceField"/>, stored as text (see <see cref="FieldValues"/>).</summary>
public sealed class FieldValue
{
    private FieldValue()
    {
    }

    public long WorkItemId { get; private set; }

    public long FieldId { get; private set; }

    public string Value { get; private set; } = "";

    public Guid UpdatedById { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static FieldValue Of(long workItemId, long fieldId, string value, Guid actorId, DateTimeOffset now) =>
        new() { WorkItemId = workItemId, FieldId = fieldId, Value = value, UpdatedById = actorId, UpdatedAt = now };

    public void Set(string value, Guid actorId, DateTimeOffset now)
    {
        Value = value;
        UpdatedById = actorId;
        UpdatedAt = now;
    }
}
