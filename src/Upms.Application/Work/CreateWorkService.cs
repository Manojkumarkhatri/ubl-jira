using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The Create form's lists (Jira parity pass FR-002–FR-005; research R10).</summary>
internal sealed class CreateWorkService(IAppDbContext db, IProjectAccess access, IProjectWorkflow workflow, AssigneeReads assignees,
    ICurrentUser currentUser)
    : ICreateWorkService
{
    public const int ParentLimit = 20;

    // The order the form lists types in, as Jira does.
    internal static readonly WorkItemType[] FormOrder =
    [
        WorkItemType.Epic, WorkItemType.Stage, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Milestone,
        WorkItemType.Subtask,
    ];

    public async Task<Result<CreateSpaces>> SpacesAsync(string? currentSpaceKey, CancellationToken ct)
    {
        var spaces = await access.ContributableAsync(ct);
        var options = spaces.Select(s => new CreateSpaceOption(s.Key, s.Name, s.Template, TypesFor(s.Template, s.WorkTypes, s.CanManage)))
            .ToList();
        var current = (currentSpaceKey ?? "").Trim().ToUpperInvariant();
        var defaultKey = options.Any(o => o.Key == current) ? current : await LastCreatedInAsync(spaces, ct) ?? options.FirstOrDefault()?.Key;
        return new CreateSpaces(options, defaultKey);
    }

    public async Task<Result<CreateOptions>> OptionsAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var columns = (await workflow.StatusesAsync(caller.ProjectId, ct)).Select(s => new StatusOption(s.Id, s.Name, s.Category)).ToList();
        var people = AssigneeReads.Options(await assignees.TeamAsync(caller.ProjectId, ct), caller.UserId, canContribute: true)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var containers = await db.WorkItems.AsNoTracking()
            .Where(w => w.ProjectId == caller.ProjectId && (w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage))
            .Select(w => new { w.Key, w.Title, w.Type, w.Rank, w.Number })
            .ToListAsync(ct);
        var sprints = caller.Template == ProjectTemplate.Scrum
            ? await db.Sprints.AsNoTracking()
                .Where(s => s.ProjectId == caller.ProjectId && s.State != SprintState.Completed)
                .OrderBy(s => s.Number)
                .Select(s => new SprintOption(s.Id, s.Name))
                .ToListAsync(ct)
            : [];
        return new CreateOptions(
            caller.Key,
            caller.Template,
            TypesFor(caller.Template, caller.WorkTypes, caller.CanManage),
            columns,
            people,
            containers
                .OrderBy(c => c.Type == WorkItemType.Stage ? c.Rank : "", StringComparer.Ordinal) // stages in stage order
                .ThenBy(c => c.Number)
                .Select(c => new ContainerOption(c.Key, c.Title, c.Type, IsLocked: false))
                .ToList(),
            sprints);
    }

    public async Task<Result<IReadOnlyList<CustomFieldView>>> FieldsAsync(string spaceKey, WorkItemType type, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var fields = await FieldReads.LiveFieldsAsync(db, caller.ProjectId, ct);
        var (placements, _) = await FieldReads.LayoutAsync(db, caller.ProjectId, type, caller.Template, fields, ct);
        var shown = placements.Select(p => SystemFields.FieldIdOf(p.Key)).OfType<long>().ToList();
        return fields
            .Where(f => shown.Contains(f.Id) && f.Type != FieldType.Formula)
            .OrderBy(f => shown.IndexOf(f.Id))
            .Select(f => new CustomFieldView(f.Id, SystemFields.KeyOf(f.Id), f.Name, f.Type, f.Description, f.Options, null, [], null, []))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<ParentOption>>> FindParentsAsync(string spaceKey, WorkItemType type, string text,
        CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var projectId = allowed.Value!.ProjectId;
        var candidates = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId);
        candidates = type switch
        {
            WorkItemType.Subtask => candidates.Where(w => w.ParentId == null
                && (w.Type == WorkItemType.Story || w.Type == WorkItemType.Task || w.Type == WorkItemType.Bug)),
            _ when WorkItemTypes.LevelOf(type) == WorkItemLevel.Standard =>
                candidates.Where(w => w.Type == WorkItemType.Epic || w.Type == WorkItemType.Stage),
            _ => candidates.Where(_ => false), // epics and stages have no parent
        };

        var term = (text ?? "").Trim();
        if (term.Length > 0)
        {
            var key = term.ToUpperInvariant();
            candidates = candidates.Where(w => w.Key.StartsWith(key) || w.Title.Contains(term));
        }

        IReadOnlyList<ParentOption> found = await candidates
            .OrderBy(w => w.Number)
            .Take(ParentLimit)
            .Select(w => new ParentOption(w.Key, w.Title, w.Type))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ParentOption>>.Ok(found);
    }

    /// <summary>The types the form offers: the space's, with stages only for those who manage it (FR-003).</summary>
    private static List<WorkItemType> TypesFor(ProjectTemplate template, WorkTypeSet workTypes, bool canManage) =>
        FormOrder.Where(t => WorkItemTypes.IsOffered(t, template, workTypes) && (t != WorkItemType.Stage || canManage)).ToList();

    /// <summary>The space of the caller's latest created item, among those offered (FR-002), read from the history through
    /// <c>IX_WorkItemChanges_Actor</c>.</summary>
    private async Task<string?> LastCreatedInAsync(IReadOnlyList<ContributableSpace> spaces, CancellationToken ct)
    {
        if (spaces.Count == 0 || currentUser.UserId is not { } me)
        {
            return null;
        }

        var ids = spaces.Select(s => s.ProjectId).ToList();
        var projectId = await (
                from change in db.WorkItemChanges.AsNoTracking()
                where change.ActorId == me && change.Field == WorkItemField.Created
                join item in db.WorkItems.IgnoreQueryFilters() on change.WorkItemId equals item.Id
                where ids.Contains(item.ProjectId)
                orderby change.OccurredAt descending, change.Id descending
                select (long?)item.ProjectId)
            .FirstOrDefaultAsync(ct);
        return spaces.FirstOrDefault(s => s.ProjectId == projectId)?.Key;
    }
}
