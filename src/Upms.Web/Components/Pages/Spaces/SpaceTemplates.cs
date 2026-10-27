using Upms.Application.Projects;

namespace Upms.Web.Components.Pages.Spaces;

/// <summary>A template on the "Create space" gallery, as Jira's cards show it.</summary>
/// <param name="Slug">Its part of the address: <c>create-space/{slug}</c>.</param>
public sealed record SpaceTemplateCard(string Slug, SpaceTemplate Template, string Name, string Description);

public static class SpaceTemplates
{
    public static IReadOnlyList<SpaceTemplateCard> All { get; } =
    [
        new("kanban", SpaceTemplate.Kanban, "Kanban", "Work efficiently and visualise work on a board with to do, doing, and done."),
        new("scrum", SpaceTemplate.Scrum, "Scrum", "Plan, track, and execute work using sprints and a backlog."),
        new("stage-gate", SpaceTemplate.StageGate, "Stage-gate", "Run work in stages, each ending in a gate that named approvers sign off."),
        new("blank", SpaceTemplate.Blank, "Blank space", "Start with a blank canvas"),
    ];

    public static SpaceTemplateCard? Find(string? slug) =>
        All.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
