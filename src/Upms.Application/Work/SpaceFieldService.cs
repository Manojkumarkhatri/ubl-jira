using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity.Contracts;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Space fields and work type layouts (Jira's "Configure" and "Fields"), and their values on work items.</summary>
internal sealed class SpaceFieldService(
    IAppDbContext db,
    IProjectAccess access,
    IProjectWorkflow workflow,
    IProjectTeam team,
    IUserDirectory users,
    TimeProvider time) : ISpaceFieldService
{
    // ----- Fields -----

    public async Task<Result<SpaceFieldList>> ListAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var fields = await FieldReads.LiveFieldsAsync(db, caller.ProjectId, ct);
        var usedIn = await UsedInAsync(caller.ProjectId, ct);
        var rows = SystemFields.All.Where(f => f.Key is SystemFields.Summary or SystemFields.Status
                || FieldReads.TypesOf(caller.Template, caller.WorkTypes).Any(t => SystemFields.AppliesTo(f.Key, t, caller.Template)))
            .Select(SystemRow)
            .Concat(fields.Select(f => Row(f, usedIn)))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new SpaceFieldList(caller.Key, info.Name, caller.Template, rows, FieldReads.TypesOf(caller.Template, caller.WorkTypes));
    }

    public async Task<Result<FieldRow>> CreateAsync(string spaceKey, FieldInput input, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var fields = await FieldReads.LiveFieldsAsync(db, caller.ProjectId, ct);
        if (CheckName(input.Name, fields, null) is { } taken)
        {
            return taken;
        }

        if (input.Type == FieldType.Formula && CheckFormula(input.Formula, fields, null) is { } invalidFormula)
        {
            return invalidFormula;
        }

        var created = SpaceField.Create(caller.ProjectId, input.Name, input.Type, input.Description, OptionsOf(input), input.Formula,
            caller.UserId, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.SpaceFields.Add(created.Value!);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return NameTaken(input.Name); // created at the same moment in another window
        }

        return Row(created.Value!, new Dictionary<string, List<WorkItemType>>());
    }

    public async Task<Result<FieldRow>> UpdateAsync(long fieldId, FieldInput input, byte[] expectedVersion, CancellationToken ct)
    {
        var field = await db.SpaceFields.SingleOrDefaultAsync(f => f.Id == fieldId && !f.IsDeleted, ct);
        if (field is null)
        {
            return AppError.NotFound("field");
        }

        var allowed = await access.RequireAsync(field.ProjectId, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (!field.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            return AppError.Conflict($"{field.Name} was changed by someone else. Review the latest version.");
        }

        var fields = await FieldReads.LiveFieldsAsync(db, field.ProjectId, ct);
        if (CheckName(input.Name, fields, field.Id) is { } taken)
        {
            return taken;
        }

        if (field.Type == FieldType.Formula && CheckFormula(input.Formula, fields, field.Id) is { } invalidFormula)
        {
            return invalidFormula;
        }

        var oldName = field.Name;
        var now = time.GetUtcNow();
        if (field.Change(input.Name, input.Description, OptionsOf(input), input.Formula, now) is { } invalid)
        {
            return invalid.ToAppError();
        }

        // Formulas name number fields: a renamed one keeps working.
        if (field.Type == FieldType.Number && !string.Equals(oldName, field.Name, StringComparison.Ordinal))
        {
            foreach (var formula in await db.SpaceFields.Where(f => f.ProjectId == field.ProjectId && !f.IsDeleted && f.Type == FieldType.Formula).ToListAsync(ct))
            {
                formula.RenameReference(oldName, field.Name, now);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppError.Conflict($"{oldName} was changed by someone else. Review the latest version.");
        }
        catch (DbUpdateException)
        {
            return NameTaken(input.Name);
        }

        return Row(field, await UsedInAsync(field.ProjectId, ct));
    }

    public async Task<Result> DeleteAsync(long fieldId, CancellationToken ct)
    {
        var field = await db.SpaceFields.SingleOrDefaultAsync(f => f.Id == fieldId && !f.IsDeleted, ct);
        if (field is null)
        {
            return AppError.NotFound("field");
        }

        var allowed = await access.RequireAsync(field.ProjectId, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (field.Type == FieldType.Number)
        {
            var users = (await FieldReads.LiveFieldsAsync(db, field.ProjectId, ct))
                .Where(f => f.Formula is { } expression && FormulaExpression.References(expression).Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                .Select(f => f.Name)
                .ToList();
            if (users.Count > 0)
            {
                return AppError.Rule("FieldInUse", $"The formula {string.Join(", ", users)} uses {field.Name}. Change that formula first.");
            }
        }

        var now = time.GetUtcNow();
        field.Delete(now);
        var key = SystemFields.KeyOf(field.Id);
        foreach (var layout in await db.WorkTypeLayouts.Where(l => l.ProjectId == field.ProjectId).ToListAsync(ct))
        {
            if (layout.Placements.Any(p => p.Key == key))
            {
                layout.Set(layout.Placements.Where(p => p.Key != key).ToList(), now);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppError.Conflict("A layout changed at the same moment. Try again.");
        }

        return Result.Ok();
    }

    // ----- Layouts -----

    public async Task<Result<TypeLayout>> GetLayoutAsync(string spaceKey, WorkItemType type, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (!FieldReads.TypesOf(caller.Template, caller.WorkTypes).Contains(type))
        {
            return AppError.NotFound("work type");
        }

        return await LayoutOfAsync(caller, type, ct);
    }

    public async Task<Result<TypeLayout>> SaveLayoutAsync(string spaceKey, WorkItemType type, IReadOnlyList<FieldPlacement> placements,
        byte[]? expectedVersion, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (!FieldReads.TypesOf(caller.Template, caller.WorkTypes).Contains(type))
        {
            return AppError.NotFound("work type");
        }

        var fields = await FieldReads.LiveFieldsAsync(db, caller.ProjectId, ct);
        if (CheckLayout(placements, type, caller.Template, fields) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        var layout = await db.WorkTypeLayouts.SingleOrDefaultAsync(l => l.ProjectId == caller.ProjectId && l.Type == type, ct);
        if (layout is null)
        {
            if (expectedVersion is not null)
            {
                return await LayoutConflictAsync(caller, type, ct);
            }

            db.WorkTypeLayouts.Add(WorkTypeLayout.For(caller.ProjectId, type, placements, now));
        }
        else
        {
            if (expectedVersion is null || !layout.RowVersion.AsSpan().SequenceEqual(expectedVersion))
            {
                return await LayoutConflictAsync(caller, type, ct);
            }

            layout.Set(placements, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return await LayoutConflictAsync(caller, type, ct);
        }

        return await LayoutOfAsync(caller, type, ct);
    }

    private async Task<TypeLayout> LayoutOfAsync(ProjectAccessInfo caller, WorkItemType type, CancellationToken ct)
    {
        var info = await workflow.GetBoardInfoAsync(caller.ProjectId, ct);
        var fields = await FieldReads.LiveFieldsAsync(db, caller.ProjectId, ct);
        var (placements, version) = await FieldReads.LayoutAsync(db, caller.ProjectId, type, caller.Template, fields, ct);
        var usedIn = await UsedInAsync(caller.ProjectId, ct);
        var rows = SystemFields.All.Where(f => f.Placeable && SystemFields.AppliesTo(f.Key, type, caller.Template))
            .Select(SystemRow)
            .Concat(fields.Select(f => Row(f, usedIn)))
            .ToList();
        return new TypeLayout(caller.Key, info.Name, caller.Template, type, SystemFields.TypeDescription(type), placements, rows, version,
            FieldReads.TypesOf(caller.Template, caller.WorkTypes));
    }

    private async Task<AppError> LayoutConflictAsync(ProjectAccessInfo caller, WorkItemType type, CancellationToken ct) =>
        AppError.Conflict($"Someone else saved the {WorkItemTypes.DisplayName(type).ToLowerInvariant()} layout first. Their layout is shown; make your changes again.",
            await LayoutOfAsync(caller, type, ct));

    /// <summary>Every field once, each in a section it can go in, and only fields the type can show.</summary>
    private static AppError? CheckLayout(IReadOnlyList<FieldPlacement> placements, WorkItemType type, ProjectTemplate template,
        IReadOnlyList<SpaceField> fields)
    {
        if (placements.Count > WorkTypeLayout.MaxFields)
        {
            return AppError.Validation("Layout", $"A work type can show at most {WorkTypeLayout.MaxFields} fields.");
        }

        if (placements.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != placements.Count)
        {
            return AppError.Validation("Layout", "Each field can be on the layout once.");
        }

        foreach (var placement in placements)
        {
            FieldType? customType = null;
            string name;
            if (SystemFields.FieldIdOf(placement.Key) is { } id)
            {
                var field = fields.FirstOrDefault(f => f.Id == id);
                if (field is null)
                {
                    return AppError.Rule("FieldGone", "A field on the layout was deleted a moment ago. Reload the page.");
                }

                customType = field.Type;
                name = field.Name;
            }
            else
            {
                if (SystemFields.Find(placement.Key) is not { Placeable: true } system || !SystemFields.AppliesTo(placement.Key, type, template))
                {
                    return AppError.Validation("Layout", $"This work type can't show “{placement.Key}”.");
                }

                name = system.Name;
            }

            if (!SystemFields.CanGoIn(placement.Key, customType, placement.Section))
            {
                return AppError.Validation("Layout", placement.Key == SystemFields.Description
                    ? "The description stays with the description fields."
                    : $"{name} goes with the context fields; only the description and paragraph fields go with the description.");
            }
        }

        return null;
    }

    // ----- Values -----

    public async Task<Result> SetValueAsync(string workItemKey, long fieldId, FieldValueInput value, CancellationToken ct)
    {
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Key == workItemKey, ct);
        if (item is null)
        {
            return AppError.NotFound("task");
        }

        var allowed = await access.RequireAsync(item.ProjectId, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var field = await db.SpaceFields.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fieldId && f.ProjectId == item.ProjectId && !f.IsDeleted, ct);
        if (field is null)
        {
            return AppError.NotFound("field");
        }

        var normalized = FieldValues.Normalize(field, value.Value, value.Items);
        if (!normalized.IsSuccess)
        {
            return normalized.Error!.ToAppError();
        }

        var newValue = normalized.Value;
        if (field.Type == FieldType.People)
        {
            foreach (var id in FieldValues.Items(newValue).Select(Guid.Parse))
            {
                if (!await team.CanBeAssignedAsync(item.ProjectId, id, ct))
                {
                    return AppError.Validation("Value", "Choose people who work in this space.");
                }
            }
        }

        var existing = await db.FieldValues.SingleOrDefaultAsync(v => v.WorkItemId == item.Id && v.FieldId == field.Id, ct);
        if (string.Equals(existing?.Value, newValue, StringComparison.Ordinal))
        {
            return Result.Ok();
        }

        var people = FieldValues.Items(existing?.Value).Concat(FieldValues.Items(newValue))
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        var names = field.Type == FieldType.People && people.Count > 0 ? await users.GetAsync(people, ct) : new Dictionary<Guid, UserDisplay>();
        string NameOf(Guid id) => WorkItemReads.NameOf(names, id);

        var now = time.GetUtcNow();
        var context = ChangeContext.New(allowed.Value!.UserId, now);
        item.FieldChanged(field.Name, FieldValues.Display(field, existing?.Value, NameOf), FieldValues.Display(field, newValue, NameOf), context);
        if (newValue is null)
        {
            db.FieldValues.Remove(existing!);
        }
        else if (existing is null)
        {
            db.FieldValues.Add(FieldValue.Of(item.Id, field.Id, newValue, context.ActorId, now));
        }
        else
        {
            existing.Set(newValue, context.ActorId, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return AppError.Conflict($"{item.Key} was changed by someone else at the same moment. Try again.");
        }

        return Result.Ok();
    }

    // ----- Helpers -----

    /// <summary>The work types whose saved layout shows each space field, by key.</summary>
    private async Task<Dictionary<string, List<WorkItemType>>> UsedInAsync(long projectId, CancellationToken ct)
    {
        var layouts = await db.WorkTypeLayouts.AsNoTracking().Where(l => l.ProjectId == projectId).ToListAsync(ct);
        return layouts
            .SelectMany(l => l.Placements.Select(p => (p.Key, l.Type)))
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Type).Order().ToList());
    }

    private static FieldRow SystemRow(SystemField field) => new(field.Key, field.Name, field.TypeName, field.Description, IsSystem: true);

    private static FieldRow Row(SpaceField field, IReadOnlyDictionary<string, List<WorkItemType>> usedIn) =>
        new(SystemFields.KeyOf(field.Id), field.Name, SpaceField.TypeName(field.Type), field.Description, IsSystem: false, field.Type,
            field.Options, field.Formula, field.RowVersion, usedIn.GetValueOrDefault(SystemFields.KeyOf(field.Id)));

    private static List<(string? Id, string Name)>? OptionsOf(FieldInput input) =>
        input.Options?.Select(o => (o.Id, o.Name)).ToList();

    /// <summary>A name not used by another live field of the space, nor by a system field or a number a formula can use.</summary>
    private static AppError? CheckName(string name, IReadOnlyList<SpaceField> fields, long? self)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return null; // the field reports the missing name
        }

        var reserved = SystemFields.All.Select(f => f.Name).Concat(SystemFields.FormulaNumbers);
        return fields.Any(f => f.Id != self && string.Equals(f.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            || reserved.Contains(trimmed, StringComparer.OrdinalIgnoreCase)
            ? NameTaken(trimmed)
            : null;
    }

    private static AppError NameTaken(string name) =>
        new(ErrorKind.Validation, SpaceField.NameTakenCode, $"This space already has a field called {name.Trim()}.",
            new Dictionary<string, string[]> { ["Name"] = [$"This space already has a field called {name.Trim()}."] });

    /// <summary>A formula can use the space's number fields, story points and times; not other formulas.</summary>
    private static AppError? CheckFormula(string? formula, IReadOnlyList<SpaceField> fields, long? self)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null; // the field reports the missing formula
        }

        var numbers = fields.Where(f => f.Type == FieldType.Number && f.Id != self).Select(f => f.Name).Concat(SystemFields.FormulaNumbers).ToList();
        return FormulaExpression.Check(formula, numbers) is { } invalid
            ? new AppError(ErrorKind.Validation, FormulaExpression.InvalidCode, invalid.Message,
                new Dictionary<string, string[]> { ["Formula"] = [invalid.Message] })
            : null;
    }
}
