using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>"My tasks" rows for amina: PAY-1, then WEB-1 (overdue), WEB-3 (a sub-task of WEB-1) and WEB-7.</summary>
public sealed class FakeMyTasksService : IMyTasksService
{
    private static readonly StatusOption ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly StatusOption InProgress = new(2, "In Progress", StatusCategory.InProgress);

    public List<MyTaskRow> Rows { get; } =
    [
        new("PAY", "Payroll", "PAY-1", "Close September", null, ToDo, Priority.Medium, new DateOnly(2026, 10, 5)),
        new("WEB", "Website Revamp", "WEB-1", "Design the home page", null, InProgress, Priority.High, new DateOnly(2026, 9, 26)),
        new("WEB", "Website Revamp", "WEB-3", "Wireframes", "WEB-1", ToDo, Priority.Medium, new DateOnly(2026, 9, 28)),
        new("WEB", "Website Revamp", "WEB-7", "Order the logo", null, ToDo, Priority.Low, null),
    ];

    public List<PageRequest> Requests { get; } = [];

    public Task<Result<Page<MyTaskRow>>> ListAsync(PageRequest page, CancellationToken ct)
    {
        Requests.Add(page);
        var items = Rows.Skip(page.Skip).Take(page.PageSize).ToList();
        return Task.FromResult(Result<Page<MyTaskRow>>.Ok(new Page<MyTaskRow>(items, Rows.Count, page.Page, page.PageSize)));
    }
}
