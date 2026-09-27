namespace Upms.Application.Identity.Contracts;

/// <summary>Display names for other modules (owners, creators, comment authors, history actors, team members).
/// Deactivated users are included: their names stay on their past work.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Active accounts whose display name, user name or email address contains <paramref name="term"/>,
    /// ordered by display name, at most <paramref name="take"/> (Phase 2 research R5).</summary>
    Task<IReadOnlyList<UserDisplay>> SearchActiveAsync(string term, int take, CancellationToken ct);
}

public sealed record UserDisplay(Guid Id, string DisplayName, bool IsActive, string UserName);
