using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;

namespace Upms.Application.Tests.Work;

/// <summary>Board helpers plus drawer calls for User Story 2.</summary>
public abstract class DrawerTestBase(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    protected Task<Result<WorkItemDetails>> DetailsAsync(string key) =>
        CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync(key, Ct));

    protected async Task<WorkItemDetails> RequireDetailsAsync(string key) => (await DetailsAsync(key)).ValueOrThrow();

    protected async Task<Result<WorkItemDetails>> EditAsync(string key, WorkItemEdit edit, byte[]? version = null)
    {
        version ??= (await RequireDetailsAsync(key)).Version;
        return await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.UpdateAsync(key, edit, version, Ct));
    }

    protected async Task<WorkItemDetails> AddSubtaskAsync(string parentKey, string title) =>
        (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.AddSubtaskAsync(parentKey, title, Ct))).ValueOrThrow();

    protected async Task<Page<ChangeView>> HistoryAsync(string key, PageRequest? page = null) =>
        (await CallAsync<IWorkItemService, Result<Page<ChangeView>>>(s => s.GetHistoryAsync(key, page ?? PageRequest.First, Ct))).ValueOrThrow();
}
