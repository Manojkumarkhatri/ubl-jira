namespace Upms.Application.Identity;

/// <summary>Makes changes to who is an active administrator (deactivating an account, giving or removing the
/// Administrator role) run one at a time: each takes this lock inside its transaction, so a second change waits and
/// then sees the first one's result. Two administrators removing each other can never leave none (FR-008).</summary>
public interface IAdministratorLock
{
    /// <summary>Takes the lock until the current transaction ends.</summary>
    Task AcquireAsync(CancellationToken ct);
}
