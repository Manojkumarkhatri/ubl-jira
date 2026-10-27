namespace Upms.Application.Work;

/// <summary>Makes a project's changes whose rules span several rows run one at a time (Phase 3 research R9): adding a
/// dependency (the loop check), creating, starting, completing and deleting sprints (one active sprint, sprint numbers),
/// and every stage and gate change. Each takes this lock inside its transaction, so a second change waits and then sees
/// the first one's result. Changes to other projects are not held up.</summary>
public interface IPlanLock
{
    /// <summary>Takes the project's lock until the current transaction ends.</summary>
    Task AcquireAsync(long projectId, CancellationToken ct);
}
