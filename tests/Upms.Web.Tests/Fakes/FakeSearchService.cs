using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeSearchService : ISearchService
{
    public List<SearchResultRow> Rows { get; } = [];

    public List<SearchResultRow> Recent { get; } = [];

    public List<SearchResultRow> WorkedOn { get; } = [];

    public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);

    public List<SearchQuery> Queries { get; } = [];

    public List<string> Unavailable { get; } = [];

    public static SearchResultRow Row(string key, string title, string project = "WEB") =>
        new(key, WorkItemType.Task, title, project, $"{project} project", new StatusOption(1, "To Do", StatusCategory.ToDo), null, [],
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));

    public Task<Result<string>> ResolveKeyAsync(string text, CancellationToken ct)
    {
        var key = text.Trim().ToUpperInvariant();
        return Task.FromResult(Keys.Contains(key)
            ? Result<string>.Ok(key)
            : Result<string>.Fail(new AppError(ErrorKind.NotFound, "NotFound", $"No work item {key} was found.")));
    }

    public Task<Result<IReadOnlyList<SearchResultRow>>> RecentAsync(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<SearchResultRow>>.Ok(Recent.ToList()));

    public Task<Result<Page<SearchResultRow>>> WorkedOnAsync(PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<SearchResultRow>>.Ok(new Page<SearchResultRow>(WorkedOn.ToList(), WorkedOn.Count, page.Page, 50)));

    public Task<Result<SearchResults>> SearchAsync(SearchQuery query, PageRequest page, CancellationToken ct)
    {
        Queries.Add(query);
        var items = Rows.Skip((page.Page - 1) * 50).Take(50).ToList();
        return Task.FromResult(Result<SearchResults>.Ok(new SearchResults(new Page<SearchResultRow>(items, Rows.Count, page.Page, 50), Unavailable.ToList())));
    }
}

public sealed class FakeSavedFilterService(Guid me) : ISavedFilterService
{
    private long _nextId = 1;

    public List<(SavedFilterView View, Guid Owner)> Filters { get; } = [];

    public SavedFilterView Add(string name, string query, bool mine = true, string owner = "Amina Khan")
    {
        var view = new SavedFilterView(_nextId++, name, query, owner, mine, [], [1]);
        Filters.Add((view, mine ? me : Guid.NewGuid()));
        return view;
    }

    public Task<Result<SavedFilterList>> ListAsync(CancellationToken ct) => Task.FromResult(Result<SavedFilterList>.Ok(new SavedFilterList(
        Filters.Where(f => f.View.IsMine).Select(f => f.View).ToList(), Filters.Where(f => !f.View.IsMine).Select(f => f.View).ToList())));

    public Task<Result<SavedFilterView>> GetAsync(long filterId, CancellationToken ct) =>
        Task.FromResult(Filters.FirstOrDefault(f => f.View.Id == filterId).View is { } view
            ? Result<SavedFilterView>.Ok(view)
            : Result<SavedFilterView>.Fail(AppError.NotFound("filter")));

    public Task<Result<SavedFilterView>> SaveAsync(string name, string query, CancellationToken ct) =>
        Task.FromResult(Filters.Any(f => f.View.IsMine && f.View.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
            ? Result<SavedFilterView>.Fail(AppError.Rule("FilterNameTaken", $"You already have a filter called {name.Trim()}."))
            : Result<SavedFilterView>.Ok(Add(name.Trim(), query)));

    public Task<Result<SavedFilterView>> RenameAsync(long filterId, string name, byte[] expectedVersion, CancellationToken ct) =>
        Change(filterId, v => v with { Name = name.Trim() });

    public Task<Result<SavedFilterView>> UpdateAsync(long filterId, string query, byte[] expectedVersion, CancellationToken ct) =>
        Change(filterId, v => v with { Query = query });

    public Task<Result> DeleteAsync(long filterId, CancellationToken ct)
    {
        Filters.RemoveAll(f => f.View.Id == filterId);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<SavedFilterView>> ShareAsync(long filterId, IReadOnlyList<string> projectKeys, CancellationToken ct) =>
        Change(filterId, v => v with { SharedWith = projectKeys.Select((k, i) => new ProjectRef(i + 1, k.ToUpperInvariant(), k)).ToList() });

    private Task<Result<SavedFilterView>> Change(long filterId, Func<SavedFilterView, SavedFilterView> change)
    {
        var index = Filters.FindIndex(f => f.View.Id == filterId);
        Filters[index] = (change(Filters[index].View), Filters[index].Owner);
        return Task.FromResult(Result<SavedFilterView>.Ok(Filters[index].View));
    }
}
