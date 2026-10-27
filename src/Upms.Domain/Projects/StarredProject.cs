namespace Upms.Domain.Projects;

/// <summary>A space a person starred, for "Starred" and "Spaces" in their sidebar (Jira parity pass FR-021b; research R6).
/// Personal; shown only while the person can see the space.</summary>
public sealed class StarredProject
{
    private StarredProject()
    {
    }

    public Guid UserId { get; private set; }

    public long ProjectId { get; private set; }

    public DateTimeOffset StarredAt { get; private set; }

    public static StarredProject Of(Guid userId, long projectId, DateTimeOffset now) =>
        new() { UserId = userId, ProjectId = projectId, StarredAt = now };
}
