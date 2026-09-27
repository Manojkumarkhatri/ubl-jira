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
    /// <summary>The role as people read it, for example "Project Admin".</summary>
    public static string DisplayName(this ProjectRole role) => role switch
    {
        ProjectRole.ProjectAdmin => "Project Admin",
        ProjectRole.Member => "Member",
        _ => "Viewer",
    };
}
