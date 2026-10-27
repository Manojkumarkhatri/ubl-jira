using System.Text.Json;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>Where a field sits on a work item, as Jira's work type layout.</summary>
public enum FieldSection
{
    /// <summary>The main column, under the summary: the description and other long text.</summary>
    Description,

    /// <summary>The details panel beside it.</summary>
    Context,

    /// <summary>Shown only when it has a value; otherwise under "More fields".</summary>
    HiddenWhenEmpty,
}

/// <summary>One field on a layout: a system field's key (<see cref="SystemFields"/>) or <c>field-{id}</c> for a space field.</summary>
public sealed record FieldPlacement(string Key, FieldSection Section);

/// <summary>The fields one work type shows in one space, in order (Jira's "Configure" on a work type). A type without a
/// saved layout shows <see cref="SystemFields.Defaults"/>.</summary>
public sealed class WorkTypeLayout
{
    public const int MaxFields = 100;

    private WorkTypeLayout()
    {
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public WorkItemType Type { get; private set; }

    public string PlacementsJson { get; private set; } = "[]";

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<FieldPlacement> Placements =>
        JsonSerializer.Deserialize<List<FieldPlacement>>(PlacementsJson) ?? [];

    public static WorkTypeLayout For(long projectId, WorkItemType type, IReadOnlyList<FieldPlacement> placements, DateTimeOffset now)
    {
        var layout = new WorkTypeLayout { ProjectId = projectId, Type = type };
        layout.Set(placements, now);
        return layout;
    }

    public void Set(IReadOnlyList<FieldPlacement> placements, DateTimeOffset now)
    {
        PlacementsJson = JsonSerializer.Serialize(placements);
        UpdatedAt = now;
    }
}

/// <summary>A field every space has, as the Fields page lists it.</summary>
/// <param name="Placeable">False for the summary and status, which every work item shows in their own place.</param>
public sealed record SystemField(string Key, string Name, string TypeName, string? Description, bool Placeable = true);

/// <summary>The built-in fields, which ones each work type can show, and the layout a type starts with.</summary>
public static class SystemFields
{
    public const string Summary = "summary";
    public const string Status = "status";
    public const string Description = "description";
    public const string Assignee = "assignee";
    public const string Parent = "parent";
    public const string DueDate = "dueDate";
    public const string StartDate = "startDate";
    public const string Priority = "priority";
    public const string Labels = "labels";
    public const string StoryPoints = "storyPoints";
    public const string Sprint = "sprint";
    public const string OriginalEstimate = "originalEstimate";
    public const string TimeTracking = "timeTracking";
    public const string Reporter = "reporter";

    public const string CustomPrefix = "field-";

    public static readonly IReadOnlyList<SystemField> All =
    [
        new(Summary, "Summary", "Summary", "The work item's title.", Placeable: false),
        new(Status, "Status", "Status", "The column the work item is in.", Placeable: false),
        new(Description, "Description", "Description", "Formatted text with mentions, links and images."),
        new(Assignee, "Assignee", "Assignee", "The person doing the work."),
        new(Parent, "Parent", "Parent", "The epic or stage a story, task, bug or milestone belongs to."),
        new(DueDate, "Due date", "Date picker", "When the work should be done."),
        new(StartDate, "Start date", "Date picker", "When the work is planned to start."),
        new(Priority, "Priority", "Priority", null),
        new(Labels, "Labels", "Labels", "Labels shared by every space, for search and filters."),
        new(StoryPoints, "Story point estimate", "Number", "The estimate of stories, tasks and bugs in agile spaces."),
        new(Sprint, "Sprint", "Sprint", "The sprint a card is in; cards move between sprints in the backlog."),
        new(OriginalEstimate, "Original estimate", "Original estimate", "The time the work was first expected to take."),
        new(TimeTracking, "Time tracking", "Time tracking", "The time logged and the time remaining."),
        new(Reporter, "Reporter", "Reporter", "The person who created the work item."),
    ];

    /// <summary>The number fields a formula can use besides number fields of the space: story points and times in hours.</summary>
    public static readonly IReadOnlyList<string> FormulaNumbers = ["Story point estimate", "Original estimate", "Time spent", "Remaining estimate"];

    public static SystemField? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    public static string KeyOf(long fieldId) => CustomPrefix + fieldId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static long? FieldIdOf(string key) =>
        key.StartsWith(CustomPrefix, StringComparison.Ordinal) && long.TryParse(key[CustomPrefix.Length..], out var id) ? id : null;

    /// <summary>Whether the system field means something for the work type in the space's template.</summary>
    public static bool AppliesTo(string key, WorkItemType type, ProjectTemplate template) => key switch
    {
        Parent => WorkItemTypes.LevelOf(type) == WorkItemLevel.Standard,
        StartDate => type != WorkItemType.Milestone,
        StoryPoints => WorkItemTypes.HasEstimate(type, template),
        Sprint => template == ProjectTemplate.Scrum && WorkItemTypes.IsCard(type),
        OriginalEstimate or TimeTracking => type is not (WorkItemType.Stage or WorkItemType.Milestone),
        Summary or Status => false,
        _ => Find(key) is not null,
    };

    /// <summary>The description stays in the main column; long text can go anywhere; everything else sits in the details.</summary>
    public static bool CanGoIn(string key, FieldType? customType, FieldSection section) =>
        key == Description ? section == FieldSection.Description
        : customType == FieldType.Paragraph || section != FieldSection.Description;

    /// <summary>A type's layout before a Space admin changes it: Jira's order, with the fields the type has.</summary>
    public static IReadOnlyList<FieldPlacement> Defaults(WorkItemType type, ProjectTemplate template) =>
    [
        new(Description, FieldSection.Description),
        .. new[] { Assignee, Parent, DueDate, Priority, Labels, StoryPoints, Sprint, OriginalEstimate, TimeTracking, StartDate, Reporter }
            .Where(key => AppliesTo(key, type, template))
            .Select(key => new FieldPlacement(key, FieldSection.Context)),
    ];

    /// <summary>What a work type is for, as Jira's work type page says it.</summary>
    public static string TypeDescription(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "Epics group stories, tasks and bugs that work towards a larger goal.",
        WorkItemType.Story => "Stories describe a feature from the point of view of the people who use it.",
        WorkItemType.Task => "Tasks track a distinct piece of work.",
        WorkItemType.Bug => "Bugs track problems or errors to fix.",
        WorkItemType.Subtask => "Sub-tasks split a story, task or bug into smaller steps.",
        WorkItemType.Stage => "Stages divide the plan into phases, each ending in a gate that named approvers sign off.",
        _ => "Milestones mark a key date in the plan.",
    };
}
