namespace Upms.Domain.Work;

/// <summary>Work item kinds (research R12; Phase 3 FR-007). The rules for each type (level, templates, card, estimate,
/// changes) are in <see cref="WorkItemTypes"/>. Values are stored by name.</summary>
public enum WorkItemType
{
    Task,
    Subtask,

    /// <summary>A container that groups related work in an agile project (Phase 3).</summary>
    Epic,

    Story,
    Bug,

    /// <summary>A container in a Stage-gate project; its gate separates it from the next stage (Phase 3).</summary>
    Stage,

    /// <summary>A single date in a Stage-gate project (its due date); no start date, estimate or sub-tasks.</summary>
    Milestone,
}

/// <summary>Where a type sits in the hierarchy (Phase 3 FR-008).</summary>
public enum WorkItemLevel
{
    /// <summary>An epic or a stage: holds standard items and has no parent.</summary>
    Container,

    /// <summary>A story, task, bug or milestone: may belong to one container.</summary>
    Standard,

    /// <summary>A sub-task: belongs to exactly one standard item other than a milestone.</summary>
    Subtask,
}
