namespace Upms.Domain.Projects;

/// <summary>When a person last opened a space, for "Recent" and "Spaces" in their sidebar (Jira parity pass FR-021; research
/// R6). Each person keeps their latest <see cref="Kept"/>.</summary>
public sealed class ProjectVisit
{
    public const int Kept = 20;

    private ProjectVisit()
    {
    }

    public Guid UserId { get; private set; }

    public long ProjectId { get; private set; }

    public DateTimeOffset VisitedAt { get; private set; }

    public static ProjectVisit Of(Guid userId, long projectId, DateTimeOffset now) =>
        new() { UserId = userId, ProjectId = projectId, VisitedAt = now };
}
