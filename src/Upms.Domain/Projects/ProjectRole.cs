namespace Upms.Domain.Projects;

/// <summary>What a member may do in one project (Phase 2 FR-001, data-model.md "Project roles and rights").</summary>
public enum ProjectRole
{
    /// <summary>Everything a Member can do, plus the project's details, columns and team.</summary>
    ProjectAdmin,

    /// <summary>Creates, edits, moves, assigns, schedules and comments on tasks.</summary>
    Member,

    /// <summary>Sees everything in the project and changes nothing.</summary>
    Viewer,
}

public static class ProjectRoleNames
{
    /// <summary>The role a person holds in the spaces they are added to, from their ticks (owner's decision of 2026-10-20):
    /// "Manage space settings" makes them a Space admin, "Create and edit work" a Member, neither a Viewer.</summary>
    public static ProjectRole For(Upms.Domain.Common.Rights rights) =>
        Upms.Domain.Common.RightsCatalog.Has(rights, Upms.Domain.Common.Rights.ManageSpaces) ? ProjectRole.ProjectAdmin
        : Upms.Domain.Common.RightsCatalog.Has(rights, Upms.Domain.Common.Rights.EditWork) ? ProjectRole.Member
        : ProjectRole.Viewer;

    /// <summary>The access as people read it, for example "Manages the space"; since the five user types (owner's decision of
    /// 2026-10-20) the role is no longer a kind of person.</summary>
    public static string DisplayName(this ProjectRole role) => role switch
    {
        ProjectRole.ProjectAdmin => "Manages the space",
        ProjectRole.Member => "Works on items",
        _ => "Looks only",
    };
}
