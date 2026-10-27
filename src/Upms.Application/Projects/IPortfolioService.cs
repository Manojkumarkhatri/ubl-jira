using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;

namespace Upms.Application.Projects;

/// <summary>Departments' portfolios and the spaces in them (owner's decision of 2026-10-20). An Admin manages all of them; a
/// Head with "Set people's type and ticks" those of their department. Leads may put the spaces they manage into the
/// portfolios they lead.</summary>
public interface IPortfolioService
{
    /// <summary>The departments, portfolios and spaces the caller can see or manage.</summary>
    Task<Result<OrganizationView>> OverviewAsync(CancellationToken ct);

    Task<Result<long>> CreateAsync(long departmentId, string name, string? description, CancellationToken ct);

    Task<Result> EditAsync(long portfolioId, string name, string? description, CancellationToken ct);

    /// <summary>Its spaces stay, in no portfolio; its leads lose the access it gave them.</summary>
    Task<Result> DeleteAsync(long portfolioId, CancellationToken ct);

    Task<Result> SetLeadsAsync(long portfolioId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Puts a space into a portfolio, or into none.</summary>
    Task<Result> MoveSpaceAsync(string spaceKey, long? portfolioId, CancellationToken ct);

    /// <summary>The portfolios the caller can put a space into.</summary>
    Task<IReadOnlyList<PortfolioChoice>> ChoicesAsync(CancellationToken ct);

    /// <summary>Active people whose name, user name or email contains <paramref name="term"/>, to choose leads; for those who
    /// manage portfolios.</summary>
    Task<IReadOnlyList<PersonRef>> FindPeopleAsync(string term, CancellationToken ct);

    /// <summary>Every MIS the caller may download, from everything down to single spaces.</summary>
    Task<Result<MisChoices>> MisChoicesAsync(CancellationToken ct);
}

/// <param name="CanCreateDepartments">An Admin, who also sees the spaces in no portfolio.</param>
public sealed record OrganizationView(IReadOnlyList<DepartmentView> Departments, IReadOnlyList<SpaceRow> Unassigned,
    bool CanCreateDepartments);

public sealed record DepartmentView(long Id, string Name, IReadOnlyList<PersonRef> Heads, IReadOnlyList<PortfolioView> Portfolios,
    bool CanManage);

public sealed record PortfolioView(long Id, long DepartmentId, string Name, string? Description, IReadOnlyList<PersonRef> Leads,
    IReadOnlyList<SpaceRow> Spaces, bool CanManage);

public sealed record SpaceRow(long Id, string Key, string Name, ProjectTemplate Template);

public sealed record PersonRef(Guid Id, string DisplayName, UserType Type);

public sealed record PortfolioChoice(long Id, string Name, string Department);

/// <param name="All">True when the caller may download the MIS of everything.</param>
public sealed record MisChoices(bool All, IReadOnlyList<MisChoice> Departments, IReadOnlyList<MisChoice> Portfolios,
    IReadOnlyList<MisChoice> Spaces);

/// <param name="Id">The department's or portfolio's id; null for a space.</param>
/// <param name="Key">The space's key; null for a department or portfolio.</param>
/// <param name="Parent">The department of a portfolio, the portfolio of a space.</param>
public sealed record MisChoice(MisLevel Level, long? Id, string? Key, string Name, string? Parent, int Spaces);
