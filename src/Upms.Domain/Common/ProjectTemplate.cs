namespace Upms.Domain.Common;

/// <summary>How a project works (Phase 3 FR-001, research R2): Kanban is continuous flow on a board, Scrum adds a
/// backlog and sprints, and Stage-gate moves work through stages separated by approval gates. Kanban and Scrum projects
/// are agile projects. Shared by the Projects and Work modules, like <see cref="StatusCategory"/>.</summary>
public enum ProjectTemplate
{
    Kanban,
    Scrum,
    StageGate,
}
