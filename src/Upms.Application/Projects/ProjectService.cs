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
                Role = db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == user.UserId)
                    .Select(m => (ProjectRole?)m.Role).FirstOrDefault(),
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
            openStatuses.Where(s => s.ProjectId == p.Id).Sum(s => counts.GetValueOrDefault(s.Id))));
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
        var taken = (await db.Projects.AsNoTracking()
                .Where(p => p.Key.StartsWith(prefix))
                .Select(p => p.Key)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        return ProjectKeySuggester.Suggest(projectName, taken.Contains);
    }

    public async Task<Result<string>> CreateAsync(string name, string key, string? description, CancellationToken ct)
    {
        if (await caller.GetAsync(ct) is not { IsActive: true } user)
        {
            return AppError.Forbidden();
        }

        var created = Project.Create(name, key, description, user.UserId, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        var project = created.Value!;
        if (await DuplicateOfAsync(project.Key, project.NormalizedName, null, ct) is { } duplicate)
        {
            return duplicate;
        }

        db.Projects.Add(project);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Someone created the same key or name at the same moment.
            db.Projects.Entry(project).State = EntityState.Detached;
            if (await DuplicateOfAsync(project.Key, project.NormalizedName, null, ct) is { } raced)
            {
                return raced;
            }

            throw;
        }

        return project.Key;
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
        AppError.Conflict("The project details were changed by someone else. Review the latest values; your text is kept.",
            await DetailsAsync(allowed, ct));

    private async Task<ProjectDetails> DetailsAsync(ProjectAccessInfo allowed, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == allowed.ProjectId, ct);
        var owners = await users.GetAsync([project.OwnerId], ct);
        return new ProjectDetails(project.Key, project.Name, project.Description,
            owners.TryGetValue(project.OwnerId, out var owner) ? owner.DisplayName : "Unknown user",
            allowed.CanManage, project.DetailsVersion, project.CreatedAt);
    }

    private async Task<AppError?> DuplicateOfAsync(string? key, string normalizedName, long? exceptProjectId, CancellationToken ct)
    {
        if (key is not null && await db.Projects.AnyAsync(p => p.Key == key, ct))
        {
            return AppError.Rule(ErrorCodes.DuplicateProjectKey, $"The key {key} is already used by another project.");
        }

        if (await db.Projects.AnyAsync(p => p.NormalizedName == normalizedName && p.Id != exceptProjectId, ct))
        {
            return AppError.Rule(ErrorCodes.DuplicateProjectName, "Another project already has this name.");
        }

        return null;
    }
}
