using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Projects;

/// <summary>The fields a form asks for besides the summary, which it always asks for.</summary>
[Flags]
public enum FormFields
{
    None = 0,
    Description = 1,
    Priority = 2,
    DueDate = 4,
    StartDate = 8,
    Labels = 16,
}

/// <summary>A form of the space, as Jira's Forms: people fill it in and each response becomes a work item of the form's
/// type in the space's first "to do" column.</summary>
public sealed class SpaceForm
{
    public const int NameMaxLength = 100;
    public const int IntroMaxLength = 1_000;

    private SpaceForm()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public string Name { get; private set; } = "";

    /// <summary>Plain text shown above the fields.</summary>
    public string? Intro { get; private set; }

    public WorkItemType WorkType { get; private set; } = WorkItemType.Task;

    public FormFields Fields { get; private set; } = FormFields.Description;

    /// <summary>Whether it takes responses; a closed form keeps its settings.</summary>
    public bool IsOpen { get; private set; } = true;

    public int ResponseCount { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DomainResult<SpaceForm> Create(long projectId, string name, WorkItemType workType, Guid userId, DateTimeOffset now)
    {
        var form = new SpaceForm { ProjectId = projectId, CreatedById = userId, CreatedAt = now };
        if (form.Change(name, null, workType, FormFields.Description, true, now) is { } error)
        {
            return error;
        }

        return form;
    }

    public DomainError? Change(string name, string? intro, WorkItemType workType, FormFields fields, bool isOpen, DateTimeOffset now)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > NameMaxLength)
        {
            return DomainError.Invalid("Name", $"Enter a name of up to {NameMaxLength} characters.");
        }

        var text = string.IsNullOrWhiteSpace(intro) ? null : intro.Trim();
        if (text is { Length: > IntroMaxLength })
        {
            return DomainError.Invalid("Intro", $"The description can have at most {IntroMaxLength:N0} characters.");
        }

        if (workType is WorkItemType.Subtask or WorkItemType.Stage)
        {
            return DomainError.Invalid("WorkType", "Choose a type that can be created on its own.");
        }

        Name = trimmed;
        Intro = text;
        WorkType = workType;
        Fields = fields & (FormFields.Description | FormFields.Priority | FormFields.DueDate | FormFields.StartDate | FormFields.Labels);
        IsOpen = isOpen;
        UpdatedAt = now;
        return null;
    }

    public void Responded() => ResponseCount++;

    public const int SpaceFieldsMaxLength = 1_000;

    /// <summary>The space fields the form also asks for, by id, comma separated (live test, 2026-10-28: Jira's forms can ask
    /// for a space's own fields).</summary>
    public string? SpaceFieldIds { get; private set; }

    /// <summary>The space fields the form asks for, in the order chosen.</summary>
    public IReadOnlyList<long> SpaceFields =>
        (SpaceFieldIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => long.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().ToList();

    /// <summary>Sets the space fields the form asks for; the caller passes only fields of the form's space.</summary>
    public void AskForSpaceFields(IEnumerable<long> fieldIds)
    {
        var text = string.Join(',', fieldIds.Distinct().Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        SpaceFieldIds = text.Length == 0 ? null : text[..Math.Min(text.Length, SpaceFieldsMaxLength)];
    }
}
