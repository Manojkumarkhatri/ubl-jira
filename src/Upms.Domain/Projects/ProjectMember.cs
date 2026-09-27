namespace Upms.Domain.Projects;

/// <summary>A person's membership in a project, owned by the <see cref="Project"/> aggregate (Phase 2 data-model.md,
/// "ProjectMember"). A person holds at most one membership per project.</summary>
public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    internal ProjectMember(Guid userId, ProjectRole role, Guid? addedById, DateTimeOffset addedAt)
    {
        UserId = userId;
        Role = role;
        AddedById = addedById;
        AddedAt = addedAt;
    }

    public long Id { get; private set; }

    public long ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public ProjectRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    /// <summary>Who added the person; null for memberships created by the Phase 2 upgrade.</summary>
    public Guid? AddedById { get; private set; }

    internal void ChangeRole(ProjectRole role) => Role = role;
}
