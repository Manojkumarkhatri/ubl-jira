using Upms.Domain.Work;

namespace Upms.Web.Components.Pages.Settings;

/// <summary>The icon of each field, as Jira's layout and Fields page show them.</summary>
public static class FieldIcons
{
    /// <summary>The field types in the order of Jira's "Create a field" tiles.</summary>
    public static readonly IReadOnlyList<FieldType> Types =
    [
        FieldType.ShortText, FieldType.Paragraph, FieldType.Date, FieldType.Number, FieldType.Formula, FieldType.Timestamp,
        FieldType.Labels, FieldType.Dropdown, FieldType.Checkbox, FieldType.People, FieldType.Url,
    ];

    public static string For(FieldType type) => type switch
    {
        FieldType.ShortText => "text",
        FieldType.Paragraph => "paragraph",
        FieldType.Date => "calendar",
        FieldType.Number => "hash",
        FieldType.Formula => "formula",
        FieldType.Timestamp => "clock",
        FieldType.Labels => "tag",
        FieldType.Dropdown => "dropdown",
        FieldType.Checkbox => "check-square",
        FieldType.People => "person",
        _ => "link",
    };

    public static string For(string key, FieldType? type) => type is { } custom ? For(custom) : key switch
    {
        SystemFields.Description => "paragraph",
        SystemFields.Assignee or SystemFields.Reporter => "person",
        SystemFields.Parent => "spaces",
        SystemFields.DueDate or SystemFields.StartDate => "calendar",
        SystemFields.Priority => "priority",
        SystemFields.Labels => "tag",
        SystemFields.StoryPoints => "hash",
        SystemFields.Sprint => "refresh",
        SystemFields.OriginalEstimate => "clock",
        SystemFields.TimeTracking => "timer",
        SystemFields.Status => "arrow-right",
        _ => "text",
    };

    /// <summary>"epic", "subtask": the work type as it appears in addresses.</summary>
    public static string Slug(WorkItemType type) => type.ToString().ToLowerInvariant();
}
