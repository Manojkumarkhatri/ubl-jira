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

    public static WorkItemDetails Sample(Guid? commentAuthor = null) => new(
        "WEB-1", "WEB", "Website Revamp", WorkItemType.Task, "Design the home page",
        "Hero first.\nSee https://example.com/brief", Priority.High, ToDo, [ToDo, InProgress, Done], null,
        "Amina Khan", T0, T0.AddHours(1), null, CanDelete: true, [1, 2, 3],
        new Page<SubtaskView>(
        [
            new SubtaskView("WEB-2", "Wireframes", Done, Priority.Medium, [4]),
            new SubtaskView("WEB-3", "Mock-ups", ToDo, Priority.Medium, [5]),
        ], 2, 1, 50),
        new Page<CommentView>(
        [
            new CommentView(11, commentAuthor ?? Guid.NewGuid(), "Bilal Ahmed", false, "Looks good", T0.AddMinutes(10), null, false, [6]),
        ], 1, 1, 50),
        new Page<ChangeView>(
        [
            new ChangeView(T0, "Amina Khan", WorkItemField.Created, null, "To Do", null),
            new ChangeView(T0.AddMinutes(5), "Amina Khan", WorkItemField.Priority, "Medium", "High", null),
        ], 2, 1, 50),
        CanContribute: true);

    public Task<Result<WorkItemDetails>> GetAsync(string workItemKey, CancellationToken ct) =>
        Task.FromResult(workItemKey == Details.Key ? Result<WorkItemDetails>.Ok(Details) : Result<WorkItemDetails>.Fail(AppError.NotFound("task")));

    public Task<Result<WorkItemDetails>> UpdateAsync(string workItemKey, WorkItemEdit edit, byte[] expectedVersion, CancellationToken ct)
    {
        Edits.Add((workItemKey, edit, expectedVersion));
        if (NextEditResult is { } next)
        {
            return Task.FromResult(next(edit));
        }

        Details = edit switch
        {
            WorkItemEdit.Title t => Details with { Title = t.Value },
            WorkItemEdit.Description d => Details with { Description = d.Value },
            WorkItemEdit.Priority p => Details with { Priority = p.Value },
            WorkItemEdit.Status s => Details with { Status = Details.Statuses.Single(x => x.Id == s.ColumnId) },
            _ => Details,
        };
        return Task.FromResult(Result<WorkItemDetails>.Ok(Details));
    }

    public Task<Result<WorkItemDetails>> AddSubtaskAsync(string parentKey, string title, CancellationToken ct)
    {
        AddedSubtasks.Add(title);
        return Task.FromResult(Result<WorkItemDetails>.Ok(Details));
    }

    public Task<Result<WorkItemDetails>> MarkSubtaskDoneAsync(string subtaskKey, byte[] expectedVersion, CancellationToken ct)
    {
        MarkedDone.Add(subtaskKey);
        return Task.FromResult(Result<WorkItemDetails>.Ok(Details));
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
