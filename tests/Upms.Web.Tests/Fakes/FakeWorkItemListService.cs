using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>The List view of WEB for component tests; records every query and the "today" it was given.</summary>
public sealed class FakeWorkItemListService : IWorkItemListService
{
    public static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    public static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);
    public static readonly StatusOption Done = new(3, "Done", StatusCategory.Done);
    public static readonly Guid Bilal = Guid.NewGuid();

    private static readonly DateTimeOffset Updated = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    public FakeWorkItemListService(Guid me)
    {
        People =
        [
            new AssigneeOption(me, "Amina Khan", IsMe: true),
            new AssigneeOption(Bilal, "Bilal Ahmed", IsMe: false),
        ];
        Rows =
        [
            new WorkItemRow("WEB-3", "Wireframes", "WEB-1", ToDo, Priority.Medium, null, null, new DateOnly(2026, 9, 30), Updated, true),
            new WorkItemRow("WEB-2", "Write the copy", null, Done, Priority.Low, AssigneeRef.Of(Bilal, "Bilal Ahmed", canWork: true),
                null, new DateOnly(2026, 9, 20), Updated, false),
            new WorkItemRow("WEB-1", "Design the home page", null, InProgress, Priority.High, AssigneeRef.Of(me, "Amina Khan", canWork: true),
                new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 26), Updated, true),
        ];
    }

    public IReadOnlyList<AssigneeOption> People { get; }

    public List<WorkItemRow> Rows { get; set; }

    /// <summary>Overrides the total, for paging tests.</summary>
    public int? TotalCount { get; set; }

    public bool CanContribute { get; set; } = true;

    public List<(WorkItemListQuery Query, DateOnly Today)> Queries { get; } = [];

    public Task<Result<WorkItemListView>> ListAsync(string projectKey, WorkItemListQuery query, DateOnly today, CancellationToken ct)
    {
        Queries.Add((query, today));
        if (projectKey != "WEB")
        {
            return Task.FromResult(Result<WorkItemListView>.Fail(AppError.NotFound("project")));
        }

        var page = new Page<WorkItemRow>(Rows, TotalCount ?? Rows.Count, query.Page, 50);
        return Task.FromResult(Result<WorkItemListView>.Ok(new WorkItemListView("WEB", "Website Revamp", CanContribute, CanManage: false,
            CanRestoreDeleted: false, ToDo.Id, [ToDo, InProgress, Done], People, page)));
    }
}
