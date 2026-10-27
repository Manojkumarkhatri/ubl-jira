using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>Records dependency changes for component tests; each succeeds unless a test sets the next error.</summary>
public sealed class FakeDependencyService : IDependencyService
{
    public List<(string Waiting, string WaitedFor)> Added { get; } = [];

    public List<(string Waiting, string WaitedFor)> Removed { get; } = [];

    public List<ItemOption> Items { get; set; } =
    [
        new("WEB-5", WorkItemType.Task, "Build the API"),
        new("WEB-6", WorkItemType.Task, "Build the app"),
    ];

    public AppError? NextError { get; set; }

    public Task<Result<DependencyList>> AddAsync(string waitingKey, string waitedForKey, CancellationToken ct)
    {
        if (TakeError() is { } error)
        {
            return Task.FromResult(Result<DependencyList>.Fail(error));
        }

        Added.Add((waitingKey, waitedForKey));
        return Task.FromResult(Result<DependencyList>.Ok(DependencyList.Empty));
    }

    public Task<Result<DependencyList>> RemoveAsync(string waitingKey, string waitedForKey, CancellationToken ct)
    {
        Removed.Add((waitingKey, waitedForKey));
        return Task.FromResult(Result<DependencyList>.Ok(DependencyList.Empty));
    }

    public Task<Result<IReadOnlyList<ItemOption>>> FindItemsAsync(string projectKey, string term, CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<ItemOption>>.Ok(
            Items.Where(i => i.Key.Contains(term, StringComparison.OrdinalIgnoreCase) || i.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList()));

    private AppError? TakeError()
    {
        var error = NextError;
        NextError = null;
        return error;
    }
}
