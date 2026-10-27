using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>An in-memory task WEB-1 with sub-tasks, comments and history; records calls.</summary>
public sealed class FakeWorkItemService : IWorkItemService
{
    public static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    public static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);
    public static readonly StatusOption Done = new(3, "Done", StatusCategory.Done);
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    public WorkItemDetails Details { get; set; } = Sample();

    public List<(string Key, WorkItemEdit Edit, byte[] Version)> Edits { get; } = [];

    public List<string> AddedSubtasks { get; } = [];

    public List<string> MarkedDone { get; } = [];

    public List<string> Deleted { get; } = [];

    public Func<WorkItemEdit, Result<WorkItemDetails>>? NextEditResult { get; set; }

    public int Loads { get; private set; }

    /// <summary>Work items created from the list, the backlog or a container's drawer (Phase 3 FR-010).</summary>
    public List<(string ProjectKey, NewWorkItem Item)> Created { get; } = [];

    public Func<NewWorkItem, Result<WorkItemDetails>>? NextCreateResult { get; set; }

    /// <summary>The epics or stages the drawer offers (Phase 3 FR-015).</summary>
    public List<ContainerOption> Containers { get; set; } =
    [
        new("WEB-7", "Card refunds", WorkItemType.Epic, false),
        new("WEB-8", "Statements", WorkItemType.Epic, false),
    ];

    public Task<Result<WorkItemDetails>> CreateAsync(string projectKey, NewWorkItem item, CancellationToken ct)
    {
        Created.Add((projectKey, item));
        return Task.FromResult(NextCreateResult?.Invoke(item)
            ?? Result<WorkItemDetails>.Ok(Details with { Key = "WEB-9", Title = item.Title, Type = item.Type }));
    }

    public Task<Result<IReadOnlyList<ContainerOption>>> ListContainersAsync(string projectKey, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<ContainerOption>>.Ok(Containers));

    public static WorkItemDetails Sample(Guid? commentAuthor = null) => new(
        "WEB-1", "WEB", "Website Revamp", WorkItemType.Task, "Design the home page",
        Rich.Plain("Hero first.\nSee https://example.com/brief"), Priority.High, ToDo, [ToDo, InProgress, Done], null,
        "Amina Khan", T0, T0.AddHours(1), null, CanDelete: true, [1, 2, 3],
        new Page<SubtaskView>(
        [
            new SubtaskView("WEB-2", "Wireframes", Done, Priority.Medium, [4], null, null),
            new SubtaskView("WEB-3", "Mock-ups", ToDo, Priority.Medium, [5], null, null),
        ], 2, 1, 50),
        new Page<CommentView>(
        [
            new CommentView(11, commentAuthor ?? Guid.NewGuid(), "Bilal Ahmed", false, Rich.Plain("Looks good"), T0.AddMinutes(10), null, false, [6]),
        ], 1, 1, 50),
        new Page<ChangeView>(
        [
            new ChangeView(T0, "Amina Khan", WorkItemField.Created, null, "To Do", null),
            new ChangeView(T0.AddMinutes(5), "Amina Khan", WorkItemField.Priority, "Medium", "High", null),
        ], 2, 1, 50),
        null, null, null, [], CanContribute: true);

    /// <summary>The item with its type's default layout, as the service reads it when no Space admin has changed it.</summary>
    public static WorkItemDetails Laid(WorkItemDetails details)
    {
        if (details.Fields != ItemFields.Empty)
        {
            return details;
        }

        var placements = SystemFields.Defaults(details.Type, details.Template);
        List<string> Keys(FieldSection section) => placements.Where(p => p.Section == section).Select(p => p.Key).ToList();
        return details with { Fields = new ItemFields(Keys(FieldSection.Description), Keys(FieldSection.Context), Keys(FieldSection.HiddenWhenEmpty)) };
    }

    private static Result<WorkItemDetails> Laid(Result<WorkItemDetails> result) => result switch
    {
        { IsSuccess: true } => Result<WorkItemDetails>.Ok(Laid(result.Value!), result.Warnings),
        { Error: { Current: WorkItemDetails latest } error } => error with { Current = Laid(latest) },
        _ => result,
    };

    public Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct)
    {
        Loads++;
        return Task.FromResult(workItemKey == Details.Key ? Result<WorkItemDetails>.Ok(Laid(Details)) : Result<WorkItemDetails>.Fail(AppError.NotFound("task")));
    }

    public Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit, byte[] expectedVersion, CancellationToken ct)
    {
        Edits.Add((workItemKey, edit, expectedVersion));
        if (NextEditResult is { } next)
        {
            return Task.FromResult(Laid(next(edit)));
        }

        Details = edit switch
        {
            WorkItemEdit.Title t => Details with { Title = t.Value },
            WorkItemEdit.Description d => Details with { Description = Rich.FromDocument(d.Value) },
            WorkItemEdit.Priority p => Details with { Priority = p.Value },
            WorkItemEdit.Status s => Details with { Status = Details.Statuses.Single(x => x.Id == s.ColumnId) },
            WorkItemEdit.Assignee a => Details with
            {
                Assignee = a.UserId is { } id
                    ? AssigneeRef.Of(id, Details.AssigneeOptions.Single(o => o.UserId == id).DisplayName, canWork: true)
                    : null,
            },
            WorkItemEdit.Dates d => Details with { StartDate = d.Start, DueDate = d.Due },
            WorkItemEdit.Type t => Details with { Type = t.Value },
            WorkItemEdit.Estimate e => Details with { Estimate = e.Value },
            WorkItemEdit.Color c => Details with { Color = c.Value },
            WorkItemEdit.Container c => Details with
            {
                Container = c.ContainerKey is { } key
                    ? Containers.Where(o => o.Key == key).Select(o => new ContainerView(o.Key, o.Title, o.Type)).Single()
                    : null,
            },
            _ => Details,
        };
        return Task.FromResult(Result<WorkItemDetails>.Ok(Laid(Details)));
    }

    public Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title, CancellationToken ct)
    {
        AddedSubtasks.Add(title);
        return Task.FromResult(Result<WorkItemDetails>.Ok(Laid(Details)));
    }

    public Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion, CancellationToken ct)
    {
        MarkedDone.Add(subtaskKey);
        return Task.FromResult(Result<WorkItemDetails>.Ok(Laid(Details)));
    }

    public Task<Result<DeletePreview>> PreviewDeleteAsync(string workItemKey, CancellationToken ct) =>
        Task.FromResult(Result<DeletePreview>.Ok(new DeletePreview(workItemKey, Details.Title, Details.Subtasks.TotalCount)));

    public Task<Result> DeleteAsync(string workItemKey, CancellationToken ct)
    {
        Deleted.Add(workItemKey);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<Page<DeletedItemView>>> ListDeletedAsync(string projectKey, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<DeletedItemView>>.Ok(Page<DeletedItemView>.Empty(page)));

    public Task<Result> RestoreAsync(string workItemKey, CancellationToken ct) => Task.FromResult(Result.Ok());

    public Task<Result<Page<SubtaskView>>> ListSubtasksAsync(string parentKey, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<SubtaskView>>.Ok(Details.Subtasks));

    public Task<Result<Page<ChangeView>>> GetHistoryAsync(string workItemKey, PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<ChangeView>>.Ok(Details.History));
}
