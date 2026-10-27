using Upms.Application.Common.Results;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A space's fields and each work type's layout, as Jira's "Configure" on a work type and its "Fields" page. Space
/// admins and administrators manage them; contributors fill the values in on work items.</summary>
public interface ISpaceFieldService
{
    /// <summary>The system fields and the space's own fields (Manage right).</summary>
    Task<Result<SpaceFieldList>> ListAsync(string spaceKey, CancellationToken ct);

    /// <summary>A new field (<c>FieldNameTaken</c>, <c>InvalidFormula</c>); it shows on no work type until placed.</summary>
    Task<Result<FieldRow>> CreateAsync(string spaceKey, FieldInput input, CancellationToken ct);

    /// <summary>Renames a field, changes its description, choices or formula; its type stays.</summary>
    Task<Result<FieldRow>> UpdateAsync(long fieldId, FieldInput input, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Takes the field off every work type; values stay for history (<c>FieldInUse</c> when a formula uses it).</summary>
    Task<Result> DeleteAsync(long fieldId, CancellationToken ct);

    /// <summary>The fields a work type shows, in order, and every field it could show (Manage right).</summary>
    Task<Result<TypeLayout>> GetLayoutAsync(string spaceKey, WorkItemType type, CancellationToken ct);

    /// <summary>Saves the type's layout; <c>Conflict</c> with the latest one when someone else saved first.</summary>
    Task<Result<TypeLayout>> SaveLayoutAsync(string spaceKey, WorkItemType type, IReadOnlyList<FieldPlacement> placements,
        byte[]? expectedVersion, CancellationToken ct);

    /// <summary>Sets or clears a work item's value for a space field (Contribute right); the history records it.</summary>
    Task<Result> SetValueAsync(string workItemKey, long fieldId, FieldValueInput value, CancellationToken ct);
}

public sealed record FieldInput(FieldType Type, string Name, string? Description = null, IReadOnlyList<FieldOptionInput>? Options = null,
    string? Formula = null);

/// <param name="Id">An existing option's id, or null for a new one.</param>
public sealed record FieldOptionInput(string? Id, string Name);

/// <param name="Value">A single value as text (see <see cref="FieldValues"/>), or null to clear it.</param>
/// <param name="Items">The items of a labels, checkbox or people field.</param>
public sealed record FieldValueInput(string? Value, IReadOnlyList<string>? Items = null);

public sealed record SpaceFieldList(string SpaceKey, string SpaceName, ProjectTemplate Template, IReadOnlyList<FieldRow> Fields,
    IReadOnlyList<WorkItemType> Types);

/// <summary>A field as the Fields page and a layout's panel list it.</summary>
/// <param name="UsedIn">The work types that show it.</param>
public sealed record FieldRow(
    string Key,
    string Name,
    string TypeName,
    string? Description,
    bool IsSystem,
    FieldType? Type = null,
    IReadOnlyList<FieldOption>? Options = null,
    string? Formula = null,
    byte[]? Version = null,
    IReadOnlyList<WorkItemType>? UsedIn = null)
{
    public IReadOnlyList<FieldOption> Options { get; init; } = Options ?? [];

    public IReadOnlyList<WorkItemType> UsedIn { get; init; } = UsedIn ?? [];

    public long? FieldId => SystemFields.FieldIdOf(Key);
}

/// <param name="Fields">Every field this type could show, placed or not.</param>
/// <param name="Version">Null while the type still shows the default layout.</param>
/// <param name="Types">The space's work types, for moving between their layouts.</param>
public sealed record TypeLayout(
    string SpaceKey,
    string SpaceName,
    ProjectTemplate Template,
    WorkItemType Type,
    string TypeDescription,
    IReadOnlyList<FieldPlacement> Placements,
    IReadOnlyList<FieldRow> Fields,
    byte[]? Version,
    IReadOnlyList<WorkItemType> Types);

/// <summary>The fields a work item shows, by key, in its type's order (<see cref="SystemFields"/> keys and <c>field-{id}</c>).</summary>
public sealed record ItemFields(IReadOnlyList<string> Description, IReadOnlyList<string> Context, IReadOnlyList<string> HiddenWhenEmpty)
{
    public static ItemFields Empty { get; } = new([], [], []);

    public bool Shows(string key) => Description.Contains(key) || Context.Contains(key) || HiddenWhenEmpty.Contains(key);
}

/// <summary>A space field on a work item, with its value.</summary>
/// <param name="Value">A single value as stored, or a formula's result.</param>
/// <param name="Items">A list value's items: labels, option ids or people's ids.</param>
/// <param name="Display">The value in words.</param>
public sealed record CustomFieldView(
    long Id,
    string Key,
    string Name,
    FieldType Type,
    string? Description,
    IReadOnlyList<FieldOption> Options,
    string? Value,
    IReadOnlyList<string> Items,
    string? Display,
    IReadOnlyList<FieldPerson> People)
{
    public bool IsEmpty => Value is null;
}

public sealed record FieldPerson(Guid UserId, string Name);
