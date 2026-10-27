using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>The rules of each work item type (Phase 3 data-model.md, "WorkItemType"; research R4): its level, the
/// templates that offer it, whether it is a card, whether it takes an estimate, and which types it can change into.</summary>
public static class WorkItemTypes
{
    /// <summary>The types that appear on boards, in the backlog and in sprints (FR-013).</summary>
    public static readonly IReadOnlyList<WorkItemType> CardTypes = [WorkItemType.Task, WorkItemType.Story, WorkItemType.Bug];

    public static bool IsAgile(ProjectTemplate template) => template is ProjectTemplate.Kanban or ProjectTemplate.Scrum;

    public static WorkItemLevel LevelOf(WorkItemType type) => type switch
    {
        WorkItemType.Epic or WorkItemType.Stage => WorkItemLevel.Container,
        WorkItemType.Subtask => WorkItemLevel.Subtask,
        _ => WorkItemLevel.Standard,
    };

    /// <summary>Kanban and Scrum offer epics, stories, tasks, bugs and sub-tasks; Stage-gate offers stages, tasks,
    /// milestones and sub-tasks (FR-007).</summary>
    public static bool IsInTemplate(WorkItemType type, ProjectTemplate template) => type switch
    {
        WorkItemType.Task or WorkItemType.Subtask => true,
        WorkItemType.Epic or WorkItemType.Story or WorkItemType.Bug => IsAgile(template),
        _ => template == ProjectTemplate.StageGate,
    };

    /// <summary>The template's types that the space has turned on; sub-tasks always, and Stage-gate spaces ignore the set
    /// (Jira parity pass FR-017).</summary>
    public static bool IsOffered(WorkItemType type, ProjectTemplate template, WorkTypeSet workTypes) =>
        IsInTemplate(type, template)
        && (!IsAgile(template) || WorkTypeSets.FlagOf(type) is var flag && (flag == WorkTypeSet.None || workTypes.HasFlag(flag)));

    /// <summary>Why a type cannot be created in a space, in the space's words, or null when it can.</summary>
    public static string? RefusalFor(WorkItemType type, ProjectTemplate template, WorkTypeSet workTypes) =>
        !IsInTemplate(type, template) ? $"This space's template does not use {PluralName(type)}."
        : !IsOffered(type, template, workTypes) ? $"This space no longer uses {PluralName(type)}."
        : null;

    public static bool IsCard(WorkItemType type) => type is WorkItemType.Task or WorkItemType.Story or WorkItemType.Bug;

    /// <summary>Stories, tasks and bugs of agile projects take an estimate (FR-014).</summary>
    public static bool HasEstimate(WorkItemType type, ProjectTemplate template) => IsCard(type) && IsAgile(template);

    /// <summary>The types an item can change into: Story, Task and Bug among themselves, in agile projects only
    /// (FR-011). Empty when the type cannot change.</summary>
    public static IReadOnlyList<WorkItemType> ChoicesFor(WorkItemType type, ProjectTemplate template) =>
        IsCard(type) && IsAgile(template) ? CardTypes : [];

    public static bool CanChange(WorkItemType from, WorkItemType to, ProjectTemplate template) =>
        ChoicesFor(from, template).Contains(from) && ChoicesFor(to, template).Contains(to);

    /// <summary>The type's name as people read it.</summary>
    public static string DisplayName(WorkItemType type) => type == WorkItemType.Subtask ? "Sub-task" : type.ToString();

    /// <summary>"Bugs", "Stories", "Sub-tasks".</summary>
    public static string PluralName(WorkItemType type) => type == WorkItemType.Story ? "Stories" : DisplayName(type) + "s";
}

/// <summary>How work item types map to a space's <see cref="WorkTypeSet"/>.</summary>
public static class WorkTypeSets
{
    /// <summary>The flag that turns the type on or off, or <see cref="WorkTypeSet.None"/> for types that cannot be turned
    /// off (sub-tasks and the Stage-gate types).</summary>
    public static WorkTypeSet FlagOf(WorkItemType type) => type switch
    {
        WorkItemType.Epic => WorkTypeSet.Epic,
        WorkItemType.Story => WorkTypeSet.Story,
        WorkItemType.Task => WorkTypeSet.Task,
        WorkItemType.Bug => WorkTypeSet.Bug,
        _ => WorkTypeSet.None,
    };
}
