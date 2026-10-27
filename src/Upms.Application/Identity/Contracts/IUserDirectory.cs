using Upms.Domain.Common;

namespace Upms.Application.Identity.Contracts;

/// <summary>Display names for other modules (owners, creators, comment authors, history actors, team members).
/// Deactivated users are included: their names stay on their past work.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserDisplay>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Active accounts whose display name, user name or email address contains <paramref name="term"/>,
    /// ordered by display name, at most <paramref name="take"/> (Phase 2 research R5).</summary>
    Task<IReadOnlyList<UserDisplay>> SearchActiveAsync(string term, int take, CancellationToken ct);

    /// <summary>What the Notifications module needs to email an event's recipients: the address on the account now, whether
    /// it is active, and the person's email choice (Phase 4 FR-049, research R11).</summary>
    Task<IReadOnlyDictionary<Guid, RecipientInfo>> GetRecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>People's type, department and ticks, for the roles they hold in spaces (owner's decision of 2026-10-20).</summary>
    Task<IReadOnlyDictionary<Guid, PersonAccess>> AccessOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>The active Heads of these departments.</summary>
    Task<IReadOnlyList<PersonAccess>> HeadsOfAsync(IReadOnlyCollection<long> departmentIds, CancellationToken ct);

    /// <summary>Everyone of this type, active or not.</summary>
    Task<IReadOnlyList<Guid>> PeopleOfTypeAsync(UserType type, CancellationToken ct);

    /// <summary>Every department's name by id.</summary>
    Task<IReadOnlyDictionary<long, string>> DepartmentNamesAsync(CancellationToken ct);
}

/// <param name="IsAdministrator">An Administrator, who can see every project (Phase 4: who can see an item).</param>
/// <param name="Type">The badge under the person's name.</param>
public sealed record UserDisplay(Guid Id, string DisplayName, bool IsActive, string UserName, bool IsAdministrator = false,
    UserType Type = UserType.Member);

public sealed record RecipientInfo(Guid Id, string DisplayName, string Email, bool IsActive, Domain.Common.EmailChoice EmailChoice);

/// <param name="Rights">Their ticks now: their type's, plus their own, less those taken away.</param>
public sealed record PersonAccess(Guid UserId, UserType Type, long? DepartmentId, Rights Rights, bool IsActive);
