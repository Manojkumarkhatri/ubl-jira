namespace Upms.Application.Identity.Contracts;

/// <summary>Display names for other modules (owners, creators, comment authors, history actors).
/// Deactivated users are included: their names stay on their past work.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

public sealed record UserDisplay(Guid Id, string DisplayName, bool IsActive);
