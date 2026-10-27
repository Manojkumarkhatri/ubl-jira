using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>The templates the "Create space" gallery offers, with what each gives (Jira parity pass FR-014; research R13).
/// Blank creates a Kanban space with the creator's columns and work types.</summary>
public static class TemplateCatalog
{
    /// <summary>The columns the Blank template starts from (FR-016).</summary>
    public static IReadOnlyList<NewColumn> BlankColumns { get; } =
    [
        new("To do", StatusCategory.ToDo),
        new("In progress", StatusCategory.InProgress),
        new("Done", StatusCategory.Done),
    ];

    public static IReadOnlyList<TemplateInfo> All { get; } =
    [
        new(SpaceTemplate.Kanban, "Kanban", "Work flows across the board; no sprints. For teams that take work as it comes.",
            Columns(ProjectTemplate.Kanban), ["Epic", "Story", "Task", "Bug", "Sub-task"], ["Summary", "Board", "List", "Timeline"]),
        new(SpaceTemplate.Scrum, "Scrum", "Plan work in sprints from an ordered backlog, with estimates and sprint reports.",
            Columns(ProjectTemplate.Scrum), ["Epic", "Story", "Task", "Bug", "Sub-task"], ["Summary", "Backlog", "Board", "List", "Timeline"]),
        new(SpaceTemplate.StageGate, "Stage-gate", "For PMO teams: stages, milestones and an approval gate between stages.",
            Columns(ProjectTemplate.StageGate), ["Stage", "Task", "Milestone", "Sub-task"], ["Summary", "Board", "List", "Timeline", "Stages"]),
        new(SpaceTemplate.Blank, "Blank", "Start from three columns and choose your own columns and work types.",
            BlankColumns, ["The work types you choose", "Sub-task"], ["Summary", "Board", "List", "Timeline"]),
    ];

    private static List<NewColumn> Columns(ProjectTemplate template) =>
        Project.StartingColumns(template).Select(c => new NewColumn(c.Name, c.Category)).ToList();
}

/// <summary>A choice in the gallery.</summary>
public enum SpaceTemplate
{
    Kanban,
    Scrum,
    StageGate,
    Blank,
}

/// <param name="WorkTypes">The work types as people read them.</param>
/// <param name="Views">The space's tabs.</param>
public sealed record TemplateInfo(SpaceTemplate Template, string Name, string Description, IReadOnlyList<NewColumn> Columns,
    IReadOnlyList<string> WorkTypes, IReadOnlyList<string> Views);
