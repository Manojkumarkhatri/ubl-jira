using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Portfolios (<see cref="IPortfolioService"/>) and the spaces an MIS covers (<see cref="IPortfolioReads"/>). Every
/// change to a portfolio's spaces or leads updates who the portfolio gives access to (<see cref="ISpaceAccessSync"/>).</summary>
internal sealed class PortfolioService(
    IAppDbContext db,
    ICallerContext caller,
    IProjectAccess access,
    IUserDirectory users,
    ISpaceAccessSync sync,
    TimeProvider time) : IPortfolioService, IPortfolioReads
{
    public async Task<Result<OrganizationView>> OverviewAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var names = await users.DepartmentNamesAsync(ct);
        var portfolios = await db.Portfolios.AsNoTracking().OrderBy(p => p.NormalizedName).ToListAsync(ct);
        var leads = await db.PortfolioLeads.AsNoTracking().ToListAsync(ct);
        var ledByMe = leads.Where(l => l.UserId == me.UserId).Select(l => l.PortfolioId).ToHashSet();
        var shown = portfolios.Where(p => Sees(me, p.DepartmentId) || ledByMe.Contains(p.Id)).ToList();
        var shownIds = shown.ConvertAll(p => p.Id);
        var spaces = await db.Projects.AsNoTracking()
            .Where(p => p.PortfolioId != null && shownIds.Contains(p.PortfolioId.Value))
            .OrderBy(p => p.NormalizedName)
            .Select(p => new { PortfolioId = p.PortfolioId!.Value, p.Id, p.Key, p.Name, p.Template })
            .ToListAsync(ct);

        var departmentIds = names.Keys.Where(id => Sees(me, id) || shown.Exists(p => p.DepartmentId == id)).ToList();
        var heads = await users.HeadsOfAsync(departmentIds, ct);
        var people = await users.GetAsync(leads.Where(l => shownIds.Contains(l.PortfolioId)).Select(l => l.UserId)
            .Concat(heads.Select(h => h.UserId)).ToList(), ct);
        PersonRef Person(Guid id) => people.TryGetValue(id, out var p)
            ? new PersonRef(id, p.DisplayName, p.Type)
            : new PersonRef(id, "Unknown person", UserType.Member);

        var departments = departmentIds
            .OrderBy(id => names[id], StringComparer.CurrentCultureIgnoreCase)
            .Select(id => new DepartmentView(id, names[id],
                heads.Where(h => h.DepartmentId == id).Select(h => Person(h.UserId)).OrderBy(p => p.DisplayName).ToList(),
                shown.Where(p => p.DepartmentId == id)
                    .Select(p => new PortfolioView(p.Id, p.DepartmentId, p.Name, p.Description,
                        leads.Where(l => l.PortfolioId == p.Id).Select(l => Person(l.UserId)).OrderBy(x => x.DisplayName).ToList(),
                        spaces.Where(s => s.PortfolioId == p.Id).Select(s => new SpaceRow(s.Id, s.Key, s.Name, s.Template)).ToList(),
                        Manages(me, p.DepartmentId)))
                    .ToList(),
                Manages(me, id)))
            .ToList();

        // Spaces in no portfolio that the caller could put into one.
        IReadOnlyList<SpaceRow> unassigned = [];
        if (me.IsAdministrator || me.Type is UserType.Head or UserType.PortfolioLead)
        {
            var loose = db.Projects.AsNoTracking().Where(p => p.PortfolioId == null);
            if (!me.IsAdministrator)
            {
                loose = loose.Where(p => db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == me.UserId
                    && m.Role == ProjectRole.ProjectAdmin));
            }

            unassigned = await loose.OrderBy(p => p.NormalizedName)
                .Select(p => new SpaceRow(p.Id, p.Key, p.Name, p.Template))
                .ToListAsync(ct);
        }

        return new OrganizationView(departments, unassigned, me.IsAdministrator);
    }

    public async Task<Result<long>> CreateAsync(long departmentId, string name, string? description, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        if (!(await users.DepartmentNamesAsync(ct)).ContainsKey(departmentId))
        {
            return AppError.NotFound("department");
        }

        if (!Manages(me, departmentId))
        {
            return AppError.Forbidden("Only an Admin, or a Head of this department, can add its portfolios.");
        }

        var created = Portfolio.Create(departmentId, name, description, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        if (await DuplicateAsync(created.Value!, ct))
        {
            return Duplicate();
        }

        db.Portfolios.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        return created.Value!.Id;
    }

    public async Task<Result> EditAsync(long portfolioId, string name, string? description, CancellationToken ct)
    {
        var (portfolio, error) = await ManagedAsync(portfolioId, ct);
        if (error is not null)
        {
            return error;
        }

        if (portfolio!.Edit(name, description) is { } refused)
        {
            return refused.ToAppError();
        }

        if (await DuplicateAsync(portfolio, ct))
        {
            return Duplicate();
        }

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(long portfolioId, CancellationToken ct)
    {
        var (portfolio, error) = await ManagedAsync(portfolioId, ct);
        if (error is not null)
        {
            return error;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var projects = await db.Projects.Where(p => p.PortfolioId == portfolioId).ToListAsync(ct);
        var now = time.GetUtcNow();
        projects.ForEach(p => p.MoveToPortfolio(null, now));
        db.PortfolioLeads.RemoveRange(await db.PortfolioLeads.Where(l => l.PortfolioId == portfolioId).ToListAsync(ct));
        db.Portfolios.Remove(portfolio!);
        await db.SaveChangesAsync(ct);
        await sync.SyncSpacesAsync(projects.ConvertAll(p => p.Id), ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetLeadsAsync(long portfolioId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var (_, error) = await ManagedAsync(portfolioId, ct);
        if (error is not null)
        {
            return error;
        }

        var wanted = userIds.Distinct().ToList();
        var people = await users.AccessOfAsync(wanted, ct);
        if (wanted.Exists(id => !people.TryGetValue(id, out var p) || !p.IsActive))
        {
            return AppError.Validation("Leads", "Choose people with active accounts.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.PortfolioLeads.Where(l => l.PortfolioId == portfolioId).ToListAsync(ct);
        db.PortfolioLeads.RemoveRange(current.Where(l => !wanted.Contains(l.UserId)));
        foreach (var id in wanted.Where(id => !current.Exists(l => l.UserId == id)))
        {
            db.PortfolioLeads.Add(new PortfolioLead(portfolioId, id));
        }

        await db.SaveChangesAsync(ct);
        await sync.SyncSpacesAsync(await db.Projects.Where(p => p.PortfolioId == portfolioId).Select(p => p.Id).ToListAsync(ct), ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MoveSpaceAsync(string spaceKey, long? portfolioId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value!.ProjectId, ct);
        if (project.PortfolioId == portfolioId)
        {
            return Result.Ok();
        }

        var ledByMe = await LedByAsync(me.UserId, ct);
        var source = project.PortfolioId is { } from ? await db.Portfolios.AsNoTracking().SingleOrDefaultAsync(p => p.Id == from, ct) : null;
        var target = portfolioId is { } to ? await db.Portfolios.AsNoTracking().SingleOrDefaultAsync(p => p.Id == to, ct) : null;
        if (portfolioId is not null && target is null)
        {
            return AppError.NotFound("portfolio");
        }

        var mayLeave = source is null ? allowed.Value!.CanManage : CanTake(me, source, ledByMe);
        var mayJoin = target is null || CanTake(me, target, ledByMe);
        if (!mayLeave || !mayJoin)
        {
            return AppError.Forbidden("Only an Admin, the department's Head or the portfolio's lead can move this space.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        project.MoveToPortfolio(portfolioId, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await sync.SyncSpacesAsync([project.Id], ct);
        await transaction.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<PortfolioChoice>> ChoicesAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return [];
        }

        var names = await users.DepartmentNamesAsync(ct);
        var ledByMe = await LedByAsync(me.UserId, ct);
        return (await db.Portfolios.AsNoTracking().ToListAsync(ct))
            .Where(p => CanTake(me, p, ledByMe))
            .Select(p => new PortfolioChoice(p.Id, p.Name, names.GetValueOrDefault(p.DepartmentId, "")))
            .OrderBy(p => p.Department, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<PersonRef>> FindPeopleAsync(string term, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me || !(me.IsAdministrator || (me.Type == UserType.Head && me.Can(Rights.SetAccess)))
            || (term ?? "").Trim().Length < 2)
        {
            return [];
        }

        return (await users.SearchActiveAsync(term!, 10, ct)).Select(u => new PersonRef(u.Id, u.DisplayName, u.Type)).ToList();
    }

    public async Task<Result<MisChoices>> MisChoicesAsync(CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var names = await users.DepartmentNamesAsync(ct);
        var portfolios = await db.Portfolios.AsNoTracking().ToListAsync(ct);
        var ledByMe = await LedByAsync(me.UserId, ct);
        var visible = await access.VisibleProjectIdsAsync(ct);
        var spaces = await db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Key, p.Name, p.PortfolioId })
            .ToListAsync(ct);
        int SpacesIn(long portfolioId) => spaces.Count(s => s.PortfolioId == portfolioId);

        var departments = me.Can(Rights.DownloadDepartmentMis)
            ? names.Where(d => me.IsAdministrator || (me.Type == UserType.Head && me.DepartmentId == d.Key))
                .Select(d => new MisChoice(MisLevel.Department, d.Key, null, d.Value, null,
                    portfolios.Where(p => p.DepartmentId == d.Key).Sum(p => SpacesIn(p.Id))))
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];
        var portfolioChoices = me.Can(Rights.DownloadPortfolioMis)
            ? portfolios.Where(p => Sees(me, p.DepartmentId) || ledByMe.Contains(p.Id))
                .Select(p => new MisChoice(MisLevel.Portfolio, p.Id, null, p.Name, names.GetValueOrDefault(p.DepartmentId), SpacesIn(p.Id)))
                .OrderBy(c => c.Parent, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];
        var spaceChoices = me.Can(Rights.DownloadSpaceMis)
            ? spaces.Where(s => visible is null || visible.Contains(s.Id))
                .Select(s => new MisChoice(MisLevel.Space, null, s.Key, s.Name,
                    s.PortfolioId is { } pid ? portfolios.Find(p => p.Id == pid)?.Name : null, 1))
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];
        return new MisChoices(me.Can(Rights.DownloadAllMis), departments, portfolioChoices, spaceChoices);
    }

    public async Task<Result<MisScope>> ScopeAsync(MisLevel level, string? spaceKey, long? id, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return AppError.Forbidden();
        }

        var names = await users.DepartmentNamesAsync(ct);
        string scopeName;
        IQueryable<Project> projects;
        switch (level)
        {
            case MisLevel.Space:
                var allowed = await access.RequireAsync(spaceKey ?? "", ProjectRight.View, ct);
                if (!allowed.IsSuccess)
                {
                    return allowed.Error!;
                }

                if (!me.Can(Rights.DownloadSpaceMis))
                {
                    return NoTick("space");
                }

                var projectId = allowed.Value!.ProjectId;
                projects = db.Projects.Where(p => p.Id == projectId);
                scopeName = await projects.Select(p => p.Name).SingleAsync(ct);
                break;
            case MisLevel.Portfolio:
                var portfolio = await db.Portfolios.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
                var ledByMe = await LedByAsync(me.UserId, ct);
                if (portfolio is null || !(Sees(me, portfolio.DepartmentId) || ledByMe.Contains(portfolio.Id)))
                {
                    return AppError.NotFound("portfolio");
                }

                if (!me.Can(Rights.DownloadPortfolioMis))
                {
                    return NoTick("portfolio");
                }

                projects = db.Projects.Where(p => p.PortfolioId == portfolio.Id);
                scopeName = portfolio.Name;
                break;
            case MisLevel.Department:
                if (id is not { } departmentId || !names.ContainsKey(departmentId) || !Sees(me, departmentId))
                {
                    return AppError.NotFound("department");
                }

                if (!me.Can(Rights.DownloadDepartmentMis))
                {
                    return NoTick("department");
                }

                projects = db.Projects.Where(p => p.PortfolioId != null
                    && db.Portfolios.Any(f => f.Id == p.PortfolioId && f.DepartmentId == departmentId));
                scopeName = names[departmentId];
                break;
            default:
                if (!me.Can(Rights.DownloadAllMis))
                {
                    return NoTick("whole organization");
                }

                projects = db.Projects;
                scopeName = "All departments";
                break;
        }

        var rows = await projects.AsNoTracking()
            .OrderBy(p => p.NormalizedName)
            .Select(p => new { p.Id, p.Key, p.Name, p.Template, p.PortfolioId })
            .ToListAsync(ct);
        var ids = rows.ConvertAll(r => r.Id);
        var portfolioIds = rows.Where(r => r.PortfolioId is not null).Select(r => r.PortfolioId!.Value).Distinct().ToList();
        var portfolios = await db.Portfolios.AsNoTracking().Where(p => portfolioIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var leads = await db.PortfolioLeads.AsNoTracking().Where(l => portfolioIds.Contains(l.PortfolioId)).ToListAsync(ct);
        var admins = await db.ProjectMembers.AsNoTracking()
            .Where(m => ids.Contains(m.ProjectId) && m.Role == ProjectRole.ProjectAdmin && !m.FromPortfolio)
            .Select(m => new { m.ProjectId, m.UserId })
            .ToListAsync(ct);
        var people = await users.GetAsync(leads.Select(l => l.UserId).Concat(admins.Select(a => a.UserId)).ToList(), ct);
        IReadOnlyList<string> Names(IEnumerable<Guid> userIds) =>
            userIds.Distinct().Where(people.ContainsKey).Select(u => people[u].DisplayName)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

        var spaces = rows.ConvertAll(r =>
        {
            var portfolio = r.PortfolioId is { } pid ? portfolios.GetValueOrDefault(pid) : null;
            var leadNames = portfolio is not null && leads.Exists(l => l.PortfolioId == portfolio.Id)
                ? Names(leads.Where(l => l.PortfolioId == portfolio.Id).Select(l => l.UserId))
                : Names(admins.Where(a => a.ProjectId == r.Id).Select(a => a.UserId));
            return new ScopeSpace(r.Id, r.Key, r.Name, r.Template, portfolio?.Name,
                portfolio is null ? null : names.GetValueOrDefault(portfolio.DepartmentId), leadNames);
        });
        return new MisScope(level, scopeName, spaces);
    }

    /// <summary>An Admin sees every department; a Head sees their own.</summary>
    private static bool Sees(CallerStatus me, long departmentId) =>
        me.IsAdministrator || (me.Type == UserType.Head && me.DepartmentId == departmentId);

    /// <summary>An Admin manages every department's portfolios; a Head with "Set people's type and ticks" their own.</summary>
    private static bool Manages(CallerStatus me, long departmentId) =>
        me.IsAdministrator || (me.Type == UserType.Head && me.DepartmentId == departmentId && me.Can(Rights.SetAccess));

    /// <summary>Who may put a space into, or take it out of, a portfolio: those who manage its department, and its leads who
    /// may manage spaces.</summary>
    private static bool CanTake(CallerStatus me, Portfolio portfolio, IReadOnlySet<long> ledByMe) =>
        Manages(me, portfolio.DepartmentId) || (ledByMe.Contains(portfolio.Id) && me.Can(Rights.ManageSpaces));

    private async Task<IReadOnlySet<long>> LedByAsync(Guid userId, CancellationToken ct) =>
        (await db.PortfolioLeads.AsNoTracking().Where(l => l.UserId == userId).Select(l => l.PortfolioId).ToListAsync(ct)).ToHashSet();

    private async Task<(Portfolio? Portfolio, AppError? Error)> ManagedAsync(long portfolioId, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } me)
        {
            return (null, AppError.Forbidden());
        }

        var portfolio = await db.Portfolios.SingleOrDefaultAsync(p => p.Id == portfolioId, ct);
        if (portfolio is null || !(Sees(me, portfolio.DepartmentId) || (await LedByAsync(me.UserId, ct)).Contains(portfolioId)))
        {
            return (null, AppError.NotFound("portfolio"));
        }

        return Manages(me, portfolio.DepartmentId)
            ? (portfolio, null)
            : (null, AppError.Forbidden("Only an Admin, or a Head of this department, can change its portfolios."));
    }

    private Task<bool> DuplicateAsync(Portfolio portfolio, CancellationToken ct) =>
        db.Portfolios.AnyAsync(p => p.Id != portfolio.Id && p.DepartmentId == portfolio.DepartmentId
            && p.NormalizedName == portfolio.NormalizedName, ct);

    private static AppError Duplicate() =>
        AppError.Validation("Name", "This department already has a portfolio with this name.");

    private static AppError NoTick(string level) =>
        AppError.Forbidden($"Your rights do not include downloading the MIS of a {level}.");
}
