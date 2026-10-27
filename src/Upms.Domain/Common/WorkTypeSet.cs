namespace Upms.Domain.Common;

/// <summary>The work types a Kanban, Scrum or blank space has turned on (Jira parity pass FR-016, FR-017; research R14).
/// Sub-task is always on; Stage-gate spaces keep their fixed types and ignore the set.</summary>
[Flags]
public enum WorkTypeSet
{
    None = 0,
    Epic = 1,
    Story = 2,
    Task = 4,
    Bug = 8,
    All = Epic | Story | Task | Bug,
}
