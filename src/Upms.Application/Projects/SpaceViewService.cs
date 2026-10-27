using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;

namespace Upms.Application.Projects;

internal sealed class SpaceViewService(IAppDbContext db, IProjectAccess access, TimeProvider time) : ISpaceViewService
{
    public const int MaxShortcuts = 20;

    public async Task<Result<SpaceViews>> GetAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        return allowed.IsSuccess ? await ViewAsync(allowed.Value!, ct) : allowed.Error!;
    }

    public async Task<Result<SpaceViews>> SetViewsAsync(string spaceKey, IReadOnlyList<string> views, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var available = SpaceViewNames.AvailableFor(allowed.Value!.Template);
        var chosen = views.Select(v => v.Trim().ToLowerInvariant()).Distinct().ToList();
        if (chosen.Count == 0 || chosen.Any(v => !available.Contains(v)))
        {
            return AppError.Validation("Views", "Keep at least one view, from the views this space has.");
        }

        var project = await db.Projects.SingleAsync(p => p.Id == allowed.Value.ProjectId, ct);
        project.SetNavigationViews(chosen.SequenceEqual(SpaceViewNames.DefaultsFor(project.Template)) ? null : string.Join(',', chosen),
            time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ViewAsync(allowed.Value, ct);
    }

    public async Task<Result<SpaceViews>> AddShortcutAsync(string spaceKey, string name, string url, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (await db.SpaceShortcuts.CountAsync(s => s.ProjectId == allowed.Value!.ProjectId, ct) >= MaxShortcuts)
        {
            return AppError.Rule("TooManyShortcuts", $"A space can have up to {MaxShortcuts} shortcuts.");
        }

        var created = SpaceShortcut.Create(allowed.Value!.ProjectId, name, url, allowed.Value.UserId, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.SpaceShortcuts.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        return await ViewAsync(allowed.Value, ct);
    }

    public async Task<Result<SpaceViews>> RemoveShortcutAsync(string spaceKey, long shortcutId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        await db.SpaceShortcuts.Where(s => s.Id == shortcutId && s.ProjectId == allowed.Value!.ProjectId).ExecuteDeleteAsync(ct);
        return await ViewAsync(allowed.Value!, ct);
    }

    private async Task<SpaceViews> ViewAsync(ProjectAccessInfo caller, CancellationToken ct)
    {
        var space = await db.Projects.AsNoTracking().Where(p => p.Id == caller.ProjectId).Select(p => new { p.Name, p.NavigationViews }).SingleAsync(ct);
        var saved = space.NavigationViews;
        var available = SpaceViewNames.AvailableFor(caller.Template);
        var views = saved is null
            ? SpaceViewNames.DefaultsFor(caller.Template)
            : saved.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(available.Contains).ToList();
        var shortcuts = await db.SpaceShortcuts.AsNoTracking()
            .Where(s => s.ProjectId == caller.ProjectId)
            .OrderBy(s => s.Id)
            .Select(s => new ShortcutView(s.Id, s.Name, s.Url))
            .ToListAsync(ct);
        return new SpaceViews(views.Count == 0 ? SpaceViewNames.DefaultsFor(caller.Template) : views, available, caller.CanManage, shortcuts)
        {
            SpaceName = space.Name,
            Template = caller.Template,
            CanContribute = caller.CanContribute,
            IsAdministrator = caller.IsAdministrator,
        };
    }
}
