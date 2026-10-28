using Upms.Application.Common.Results;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>A space's Forms, as Jira's: Space admins build forms; people who can contribute fill them in, and each
/// response becomes a work item of the form's type, with the same rules as the Create form.</summary>
public interface ISpaceFormService
{
    /// <summary>The space's forms, by name (View right).</summary>
    Task<Result<SpaceFormList>> ListAsync(string spaceKey, CancellationToken ct);

    Task<Result<SpaceFormView>> GetAsync(string spaceKey, long formId, CancellationToken ct);

    /// <summary>A new form for the space's first type (Manage right).</summary>
    Task<Result<SpaceFormView>> CreateAsync(string spaceKey, string name, CancellationToken ct);

    /// <summary>Manage right; the type must be one the space offers.</summary>
    Task<Result<SpaceFormView>> SaveAsync(string spaceKey, long formId, FormSettings settings, CancellationToken ct);

    Task<Result> DeleteAsync(string spaceKey, long formId, CancellationToken ct);

    /// <summary>Creates the work item (Contribute right) and returns its key; <c>FormClosed</c> once the form stops taking
    /// responses.</summary>
    Task<Result<string>> SubmitAsync(string spaceKey, long formId, FormResponse response, CancellationToken ct);
}

/// <param name="SpaceFields">The space fields the form also asks for, by id (live test, 2026-10-28).</param>
public sealed record FormSettings(string Name, string? Intro, WorkItemType WorkType, FormFields Fields, bool IsOpen,
    IReadOnlyList<long>? SpaceFields = null);

/// <param name="Description">Plain text; it becomes the item's description.</param>
public sealed record FormResponse(string Summary, string? Description, Priority? Priority, DateOnly? StartDate, DateOnly? DueDate,
    IReadOnlyList<string>? Labels)
{
    /// <summary>Answers to the space fields the form asks for, by field id.</summary>
    public IReadOnlyDictionary<long, FieldValueInput> FieldValues { get; init; } = new Dictionary<long, FieldValueInput>();
}

/// <param name="Types">The types a form can create in this space.</param>
public sealed record SpaceFormList(bool CanManage, bool CanSubmit, IReadOnlyList<SpaceFormView> Forms, IReadOnlyList<WorkItemType> Types);

public sealed record SpaceFormView(long Id, string Name, string? Intro, WorkItemType WorkType, FormFields Fields, bool IsOpen,
    int ResponseCount, DateTimeOffset UpdatedAt, bool CanManage, bool CanSubmit, IReadOnlyList<WorkItemType> Types)
{
    /// <summary>The space fields the form asks for, in its order.</summary>
    public IReadOnlyList<CustomFieldView> AskedFields { get; init; } = [];

    /// <summary>The space fields a form can ask for: every field of the space but formulas and people, for the editor.</summary>
    public IReadOnlyList<CustomFieldView> AvailableFields { get; init; } = [];
}
