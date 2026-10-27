using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Identity.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>Reads layouts and field values: a type's layout (saved or default, without fields that no longer exist), and a
/// work item's space fields with their values, formulas worked out.</summary>
internal static class FieldReads
{
    /// <summary>The types a space has, in the order screens list them.</summary>
    public static IReadOnlyList<WorkItemType> TypesOf(ProjectTemplate template, WorkTypeSet workTypes) =>
        new[] { WorkItemType.Epic, WorkItemType.Story, WorkItemType.Task, WorkItemType.Bug, WorkItemType.Stage, WorkItemType.Milestone, WorkItemType.Subtask }
            .Where(t => WorkItemTypes.IsOffered(t, template, workTypes))
            .ToList();

    public static Task<List<SpaceField>> LiveFieldsAsync(IAppDbContext db, long projectId, CancellationToken ct) =>
        db.SpaceFields.AsNoTracking().Where(f => f.ProjectId == projectId && !f.IsDeleted).OrderBy(f => f.Name).ToListAsync(ct);

    /// <summary>The saved layout, or the default; fields that were deleted, or that the type cannot show, are left out.</summary>
    public static async Task<(IReadOnlyList<FieldPlacement> Placements, byte[]? Version)> LayoutAsync(IAppDbContext db, long projectId,
        WorkItemType type, ProjectTemplate template, IReadOnlyCollection<SpaceField> liveFields, CancellationToken ct)
    {
        var saved = await db.WorkTypeLayouts.AsNoTracking().SingleOrDefaultAsync(l => l.ProjectId == projectId && l.Type == type, ct);
        var placements = saved?.Placements ?? SystemFields.Defaults(type, template);
        var live = liveFields.Select(f => f.Id).ToHashSet();
        var shown = placements
            .Where(p => SystemFields.FieldIdOf(p.Key) is { } id ? live.Contains(id) : SystemFields.AppliesTo(p.Key, type, template))
            .ToList();
        return (shown, saved?.RowVersion);
    }

    /// <summary>The work item's layout and its space fields' values.</summary>
    public static async Task<(ItemFields Layout, IReadOnlyList<CustomFieldView> Values)> ForItemAsync(IAppDbContext db, IUserDirectory users,
        WorkItem item, ProjectTemplate template, CancellationToken ct)
    {
        var fields = await LiveFieldsAsync(db, item.ProjectId, ct);
        var (placements, _) = await LayoutAsync(db, item.ProjectId, item.Type, template, fields, ct);
        var layout = new ItemFields(
            placements.Where(p => p.Section == FieldSection.Description).Select(p => p.Key).ToList(),
            placements.Where(p => p.Section == FieldSection.Context).Select(p => p.Key).ToList(),
            placements.Where(p => p.Section == FieldSection.HiddenWhenEmpty).Select(p => p.Key).ToList());
        var shownIds = placements.Select(p => SystemFields.FieldIdOf(p.Key)).OfType<long>().ToHashSet();
        if (shownIds.Count == 0)
        {
            return (layout, []);
        }

        var stored = await db.FieldValues.AsNoTracking()
            .Where(v => v.WorkItemId == item.Id)
            .ToDictionaryAsync(v => v.FieldId, v => v.Value, ct);
        var peopleIds = fields.Where(f => f.Type == FieldType.People && stored.ContainsKey(f.Id))
            .SelectMany(f => FieldValues.Items(stored[f.Id])).Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var names = peopleIds.Count == 0 ? new Dictionary<Guid, UserDisplay>() : await users.GetAsync(peopleIds, ct);
        string NameOf(Guid id) => WorkItemReads.NameOf(names, id);

        var values = new List<CustomFieldView>();
        foreach (var field in fields.Where(f => shownIds.Contains(f.Id)))
        {
            var value = field.Type == FieldType.Formula ? Formula(field, item, fields, stored) : stored.GetValueOrDefault(field.Id);
            var items = FieldValues.Items(value);
            var people = field.Type == FieldType.People
                ? items.Select(id => Guid.TryParse(id, out var g) ? new FieldPerson(g, NameOf(g)) : null).OfType<FieldPerson>().ToList()
                : [];
            values.Add(new CustomFieldView(field.Id, SystemFields.KeyOf(field.Id), field.Name, field.Type, field.Description, field.Options,
                value, items, FieldValues.Display(field, value, NameOf), people));
        }

        return (layout, values);
    }

    /// <summary>A formula's result for the item, from its number fields, story points and times (in hours).</summary>
    public static string? Formula(SpaceField formula, WorkItem item, IReadOnlyCollection<SpaceField> fields, IReadOnlyDictionary<long, string> stored)
    {
        decimal? ValueOf(string name)
        {
            if (fields.FirstOrDefault(f => f.Type == FieldType.Number && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) is { } number)
            {
                return stored.TryGetValue(number.Id, out var text)
                    && decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
            }

            return name.ToUpperInvariant() switch
            {
                "STORY POINT ESTIMATE" => item.Estimate,
                "ORIGINAL ESTIMATE" => item.OriginalEstimateMinutes / 60m,
                "TIME SPENT" => item.TimeSpentMinutes / 60m,
                "REMAINING ESTIMATE" => item.RemainingEstimateMinutes / 60m,
                _ => null,
            };
        }

        return formula.Formula is { } expression && FormulaExpression.Evaluate(expression, ValueOf) is { } result
            ? FieldValues.FormatNumber(result)
            : null;
    }
}
