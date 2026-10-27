namespace Upms.Web.Components.Shared;

/// <summary>The pages of a project, for <see cref="ProjectHeader"/>.</summary>
public enum ProjectPage
{
    Board,
    List,
    Timeline,
    Members,
    Settings,
    Deleted,

    /// <summary>The backlog and sprints of a Scrum project, and their reports (Phase 3 FR-021).</summary>
    Backlog,

    /// <summary>The stages and gates of a Stage-gate project (Phase 3 FR-031).</summary>
    Stages,

    /// <summary>The space's Summary page (Jira parity pass FR-022).</summary>
    Summary,

    /// <summary>Work items on a month calendar, by due date.</summary>
    Calendar,

    /// <summary>Every file on the space's work items.</summary>
    Attachments,

    /// <summary>Created vs resolved and the space's breakdowns.</summary>
    Reports,

    /// <summary>Stage gates waiting for, or given, their approvals.</summary>
    Approvals,

    /// <summary>Forms whose responses become work items.</summary>
    Forms,

    /// <summary>Pages of formatted text the team keeps.</summary>
    Docs,
}
