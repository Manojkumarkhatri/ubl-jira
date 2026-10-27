using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

/// <summary>Projects (FR-011 to FR-015; Phase 2 FR-002, FR-007, FR-015).</summary>
internal sealed class ProjectService(
    IAppDbContext db,
    ICallerContext caller,
    IProjectAccess access,
    IUserDirectory users,
    IWorkItemCounts workItemCounts,
    IProjectPlanSetup planSetup,
    IPlanningAuditLog planningAudit,
    TimeProvider time) : IProjectService
{
    public async Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        page = page.Normalized();
        // Members see their projects; administrators see every project (Phase 2 FR-002, FR-006).
        var visible = user.IsAdministrator
            ? db.Projects.AsNoTracking()
            : db.Projects.AsNoTracking().Where(p => db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == user.UserId));
        var total = await visible.CountAsync(ct);
        var projects = await visible
            .OrderBy(p => p.NormalizedName)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Key,
                p.Name,
                p.Template,
                Role = db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == user.UserId)
                    .Select(m => (ProjectRole?)m.Role).FirstOrDefault(),
                IsStarred = db.StarredProjects.Any(s => s.ProjectId == p.Id && s.UserId == user.UserId),
            })
            .ToListAsync(ct);
        var projectIds = projects.ConvertAll(p => p.Id);

        // Open work = not in a "done" column; counted by the Work module (data-model.md, "Open work item").
        var openStatuses = await db.ProjectStatuses.AsNoTracking()
            .Where(s => projectIds.Contains(s.ProjectId) && s.Category != StatusCategory.Done)
            .Select(s => new { s.Id, s.ProjectId })
            .ToListAsync(ct);
        var counts = await workItemCounts.CountByStatusAsync(openStatuses.ConvertAll(s => s.Id), ct);

        var items = projects.ConvertAll(p => new ProjectSummary(
            p.Key,
            p.Name,
            p.Role,
            p.Role is null,
            openStatuses.Where(s => s.ProjectId == p.Id).Sum(s => counts.GetValueOrDefault(s.Id)),
            p.Template,
            p.IsStarred));
        return new Page<ProjectSummary>(items, total, page.Page, page.PageSize);
    }

    public async Task<string> SuggestKeyAsync(string projectName, CancellationToken ct)
    {
        var candidate = ProjectKeySuggester.Suggest(projectName);
        if (candidate.Length == 0 || await caller.GetAsync(ct) is not { IsActive: true })
        {
            return candidate;
        }

        var prefix = candidate[..Math.Min(candidate.Length, 7)];
        var taken = (await db.Projects.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.Key.StartsWith(prefix))
                .Select(p => p.Key)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        return ProjectKeySuggester.Suggest(projectName, taken.Contains);
    }

    public async Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template,
        CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        if (!user.Can(Rights.CreateSpaces))
        {
            return NoCreateTick();
        }

        var created = Project.Create(name, key, description, user.UserId, time.GetUtcNow(), template);
        return created.IsSuccess ? await SaveNewAsync(created.Value!, ct) : created.Error!.ToAppError();
    }

    public async Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        if (!user.Can(Rights.CreateSpaces))
        {
            return NoCreateTick();
        }

        var created = Project.Create(name, key, description, user.UserId, time.GetUtcNow(),
            setup.Columns.Select(c => (c.Name, c.Category)).ToList(), setup.WorkTypes);
        return created.IsSuccess ? await SaveNewAsync(created.Value!, ct) : created.Error!.ToAppError();
    }

    public async Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, WorkTypeSet workTypes, int expectedDetailsVersion,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value!.ProjectId, ct);
        if (project.DetailsVersion != expectedDetailsVersion)
        {
            return await ConflictAsync(allowed.Value!, ct);
        }

        var before = project.WorkTypes;
        if (project.SetWorkTypes(workTypes, await planSetup.TypeCountsAsync(project.Id, ct), time.GetUtcNow()) is { } refused)
        {
            return refused.ToAppError();
        }

        if (project.WorkTypes != before)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return await ConflictAsync(allowed.Value!, ct);
            }

            await planningAudit.WriteAsync(PlanningAuditEvent.WorkTypesChanged, project.Key,
                new { From = Names(before), To = Names(project.WorkTypes) }, ct);
            await transaction.CommitAsync(ct);
        }

        return await DetailsAsync(allowed.Value!, ct);
    }

    /// <summary>"Epic, Story, Task, Bug" rather than "All", for the audit log.</summary>
    private static string Names(WorkTypeSet workTypes) =>
        string.Join(", ", new[] { WorkTypeSet.Epic, WorkTypeSet.Story, WorkTypeSet.Task, WorkTypeSet.Bug }.Where(t => workTypes.HasFlag(t)));

    /// <summary>Saves a new space with its creator as Space admin, and a Stage-gate space's four stages, in one transaction.</summary>
    private static AppError NoCreateTick() =>
        AppError.Forbidden("Your ticks do not include creating spaces. Ask an Admin or your Head.");

    private async Task<Result<string>> SaveNewAsync(Project project, CancellationToken ct)
    {
        if (await DuplicateOfAsync(project.Key, project.NormalizedName, null, ct) is { } duplicate)
        {
            return duplicate;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Projects.Add(project);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone created the same key or name at the same moment.
            await transaction.RollbackAsync(ct);
            db.Projects.Entry(project).State = EntityState.Detached;
            if (await DuplicateOfAsync(project.Key, project.NormalizedName, null, ct) is { } raced)
            {
                return raced;
            }

            throw;
        }

        // A Stage-gate project starts with its four stages (Phase 3 FR-003), created with the project.
        if (project.Template == ProjectTemplate.StageGate)
        {
            await planSetup.CreateDefaultStagesAsync(project.Id, project.Key, ct);
        }

        await transaction.CommitAsync(ct);
        return project.Key;
    }

    public async Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value!.ProjectId, ct);
        if (project.DetailsVersion != expectedDetailsVersion)
        {
            return await ConflictAsync(allowed.Value!, ct);
        }

        var from = project.Template;
        var hasOpenSprints = from == ProjectTemplate.Scrum && await planSetup.HasOpenSprintsAsync(project.Id, ct);
        if (project.SwitchTemplate(target, hasOpenSprints, time.GetUtcNow()) is { } refused)
        {
            return refused.ToAppError();
        }

        if (project.Template == from)
        {
            return await DetailsAsync(allowed.Value!, ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(allowed.Value!, ct);
        }

        if (target == ProjectTemplate.Scrum)
        {
            await planSetup.PrepareForScrumAsync(project.Id, ct);
        }

        await planningAudit.WriteAsync(PlanningAuditEvent.TemplateChanged, project.Key,
            new { From = from.ToString(), To = target.ToString() }, ct);
        await transaction.CommitAsync(ct);
        return await DetailsAsync(allowed.Value! with { Template = target }, ct);
    }

    public async Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.View, ct);
        return allowed.IsSuccess ? await DetailsAsync(allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description,
        int expectedDetailsVersion, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(projectKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value!.ProjectId, ct);
        if (project.DetailsVersion != expectedDetailsVersion)
        {
            return await ConflictAsync(allowed.Value!, ct);
        }

        if (project.UpdateDetails(name, description, time.GetUtcNow()) is { } invalid)
        {
            return invalid.ToAppError();
        }

        if (await DuplicateOfAsync(null, project.NormalizedName, project.Id, ct) is { } duplicate)
        {
            return duplicate;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ConflictAsync(allowed.Value!, ct);
        }

        return await DetailsAsync(allowed.Value!, ct);
    }

    private async Task<AppError> ConflictAsync(ProjectAccessInfo allowed, CancellationToken ct) =>
        AppError.Conflict("The space details were changed by someone else. Review the latest values; your text is kept.",
            await DetailsAsync(allowed, ct));

    private async Task<ProjectDetails> DetailsAsync(ProjectAccessInfo allowed, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == allowed.ProjectId, ct);
        var owners = await users.GetAsync([project.OwnerId], ct);
        return new ProjectDetails(project.Key, project.Name, project.Description,
            owners.TryGetValue(project.OwnerId, out var owner) ? owner.DisplayName : "Unknown user",
            allowed.CanManage, project.DetailsVersion, project.CreatedAt, project.Template, project.WorkTypes,
            allowed.CanManage ? await planSetup.TypeCountsAsync(project.Id, ct) : null);
    }

    private async Task<AppError?> DuplicateOfAsync(string? key, string normalizedName, long? exceptProjectId, CancellationToken ct)
    {
        // Spaces in the trash keep their key and name, so restoring one never clashes.
        if (key is not null && await db.Projects.IgnoreQueryFilters().AnyAsync(p => p.Key == key, ct))
        {
            return AppError.Rule(ErrorCodes.DuplicateProjectKey, $"The key {key} is already used by another space.");
        }

        if (await db.Projects.IgnoreQueryFilters().AnyAsync(p => p.NormalizedName == normalizedName && p.Id != exceptProjectId, ct))
        {
            return AppError.Rule(ErrorCodes.DuplicateProjectName, "Another space already has this name.");
        }

        return null;
    }
}
