using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Work;

internal sealed class SpaceFormService(IAppDbContext db, IProjectAccess access, IWorkItemService items, ISpaceFieldService spaceFields,
    TimeProvider time)
    : ISpaceFormService
{
    public const int MaxForms = 100;

    public async Task<Result<SpaceFormList>> ListAsync(string spaceKey, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var forms = await db.SpaceForms.AsNoTracking()
            .Where(f => f.ProjectId == caller.ProjectId)
            .OrderBy(f => f.Name).ThenBy(f => f.Id)
            .Take(MaxForms)
            .ToListAsync(ct);
        return new SpaceFormList(caller.CanManage, caller.CanContribute, forms.ConvertAll(f => View(f, caller)), TypesFor(caller));
    }

    public async Task<Result<SpaceFormView>> GetAsync(string spaceKey, long formId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.View, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var form = await db.SpaceForms.AsNoTracking().SingleOrDefaultAsync(f => f.Id == formId && f.ProjectId == allowed.Value!.ProjectId, ct);
        return form is null ? AppError.NotFound("form") : await WithFieldsAsync(View(form, allowed.Value!), form, ct);
    }

    public async Task<Result<SpaceFormView>> CreateAsync(string spaceKey, string name, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        if (await db.SpaceForms.CountAsync(f => f.ProjectId == caller.ProjectId, ct) >= MaxForms)
        {
            return AppError.Rule("TooManyForms", $"A space can have up to {MaxForms} forms.");
        }

        var types = TypesFor(caller);
        var type = types.Contains(WorkItemType.Task) ? WorkItemType.Task : types.FirstOrDefault(WorkItemType.Task);
        var created = SpaceForm.Create(caller.ProjectId, name, type, caller.UserId, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error!.ToAppError();
        }

        db.SpaceForms.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        return View(created.Value!, caller);
    }

    public async Task<Result<SpaceFormView>> SaveAsync(string spaceKey, long formId, FormSettings settings, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var caller = allowed.Value!;
        var form = await db.SpaceForms.SingleOrDefaultAsync(f => f.Id == formId && f.ProjectId == caller.ProjectId, ct);
        if (form is null)
        {
            return AppError.NotFound("form");
        }

        if (!TypesFor(caller).Contains(settings.WorkType))
        {
            return AppError.Validation("WorkType", "Choose a type this space uses.");
        }

        if (form.Change(settings.Name, settings.Intro, settings.WorkType, settings.Fields, settings.IsOpen, time.GetUtcNow()) is { } error)
        {
            return error.ToAppError();
        }

        // Only fields of this space a form can ask for count, in the order chosen.
        var askable = (await AskableFieldsAsync(caller.ProjectId, ct)).Select(f => f.Id).ToHashSet();
        form.AskForSpaceFields((settings.SpaceFields ?? []).Where(askable.Contains));

        await db.SaveChangesAsync(ct);
        return await WithFieldsAsync(View(form, caller), form, ct);
    }

    public async Task<Result> DeleteAsync(string spaceKey, long formId, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Manage, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        await db.SpaceForms.Where(f => f.Id == formId && f.ProjectId == allowed.Value!.ProjectId).ExecuteDeleteAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<string>> SubmitAsync(string spaceKey, long formId, FormResponse response, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(spaceKey, ProjectRight.Contribute, ct);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        var form = await db.SpaceForms.SingleOrDefaultAsync(f => f.Id == formId && f.ProjectId == allowed.Value!.ProjectId, ct);
        if (form is null)
        {
            return AppError.NotFound("form");
        }

        if (!form.IsOpen)
        {
            return AppError.Rule("FormClosed", "This form is not taking responses.");
        }

        // Only the fields the form asks for are used, whatever else was sent.
        var asks = form.Fields;
        var created = await items.CreateAsync(spaceKey, new NewWorkItem(form.WorkType, response.Summary)
        {
            Description = asks.HasFlag(FormFields.Description) && !string.IsNullOrWhiteSpace(response.Description)
                ? FormattedText.FromPlainText(response.Description).Json
                : null,
            Priority = asks.HasFlag(FormFields.Priority) ? response.Priority : null,
            StartDate = asks.HasFlag(FormFields.StartDate) ? response.StartDate : null,
            DueDate = asks.HasFlag(FormFields.DueDate) ? response.DueDate : null,
            Labels = asks.HasFlag(FormFields.Labels) ? response.Labels : null,
        }, ct);
        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        form.Responded();
        await db.SaveChangesAsync(ct);

        // The answers to the space fields the form asks for, as the item's details would set them; an answer the field
        // refuses is left out rather than losing the response.
        foreach (var fieldId in form.SpaceFields)
        {
            if (response.FieldValues.TryGetValue(fieldId, out var value) && (value.Value is not null || value.Items is { Count: > 0 }))
            {
                await spaceFields.SetValueAsync(created.Value!.Key, fieldId, value, ct);
            }
        }

        return created.Value!.Key;
    }

    /// <summary>The space fields a form can ask for: all but formulas (worked out) and people (chosen from a team list).</summary>
    private async Task<List<SpaceField>> AskableFieldsAsync(long projectId, CancellationToken ct) =>
        (await FieldReads.LiveFieldsAsync(db, projectId, ct)).Where(f => f.Type is not (FieldType.Formula or FieldType.People)).ToList();

    private async Task<SpaceFormView> WithFieldsAsync(SpaceFormView view, SpaceForm form, CancellationToken ct)
    {
        var fields = await AskableFieldsAsync(form.ProjectId, ct);
        CustomFieldView Field(SpaceField f) =>
            new(f.Id, SystemFields.KeyOf(f.Id), f.Name, f.Type, f.Description, f.Options, null, [], null, []);
        var asked = form.SpaceFields.Select(id => fields.FirstOrDefault(f => f.Id == id)).OfType<SpaceField>().Select(Field).ToList();
        return view with { AskedFields = asked, AvailableFields = fields.Select(Field).ToList() };
    }

    /// <summary>The types the space offers that can be created on their own, in the Create form's order.</summary>
    private static List<WorkItemType> TypesFor(ProjectAccessInfo caller) =>
        CreateWorkService.FormOrder
            .Where(t => t is not (WorkItemType.Subtask or WorkItemType.Stage) && WorkItemTypes.IsOffered(t, caller.Template, caller.WorkTypes))
            .ToList();

    private static SpaceFormView View(SpaceForm form, ProjectAccessInfo caller) =>
        new(form.Id, form.Name, form.Intro, form.WorkType, form.Fields, form.IsOpen, form.ResponseCount, form.UpdatedAt, caller.CanManage,
            caller.CanContribute, TypesFor(caller));
}
