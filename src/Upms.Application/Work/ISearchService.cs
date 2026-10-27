using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects.Contracts;
using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Application.Work;

/// <summary>The search box and page (Phase 4 FR-032–FR-038, research R15, R16): results come only from projects the caller
/// can see, and never include deleted items.</summary>
public interface ISearchService
{
    /// <summary>A key the caller can see, in upper case; any other key, unknown or hidden, gets the same <c>NotFound</c>
    /// "No work item KEY was found." (FR-032); <c>Forbidden</c> when nobody active is signed in.</summary>
    Task<Result<string>> ResolveKeyAsync(string text, CancellationToken ct);

    /// <summary>The 10 items the caller opened last that they can still see (FR-033); <c>Forbidden</c> when nobody active is
    /// signed in.</summary>
    Task<Result<IReadOnlyList<SearchResultRow>>> RecentAsync(CancellationToken ct);

    /// <summary>"For you", Worked on: items the caller created, changed or commented on in the last 30 days, newest first, once
    /// each, 50 a page, only in spaces they can see (Jira parity pass FR-022a). A row's time is the caller's latest change.</summary>
    Task<Result<Page<SearchResultRow>>> WorkedOnAsync(PageRequest page, CancellationToken ct);

    /// <summary>Words and filters across every project the caller can see, 50 at a time; <c>Forbidden</c> when nobody
    /// active is signed in.</summary>
    Task<Result<SearchResults>> SearchAsync(SearchQuery query, PageRequest page, CancellationToken ct);
}

/// <summary>Calendar days, both ends included; either may be open.</summary>
public sealed record DateRange(DateOnly? From, DateOnly? To);

public enum SearchSort
{
    Updated,
    Created,
    Key,
    Priority,
    DueDate,
}

/// <summary>What the search page asks for (FR-034–FR-036). Filters of different kinds combine; the assignee filters ("me",
/// "unassigned" and people) match any of them.</summary>
public sealed record SearchQuery(
    string? Words,
    IReadOnlyList<string>? ProjectKeys = null,
    IReadOnlyList<WorkItemType>? Types = null,
    IReadOnlyList<StatusCategory>? Categories = null,
    IReadOnlyList<Priority>? Priorities = null,
    bool AssignedToMe = false,
    bool Unassigned = false,
    IReadOnlyList<Guid>? Assignees = null,
    IReadOnlyList<Guid>? Creators = null,
    IReadOnlyList<string>? Labels = null,
    DateRange? Created = null,
    DateRange? Updated = null,
    DateRange? Due = null,
    SearchSort Sort = SearchSort.Updated,
    bool Descending = true,
    DateRange? Resolved = null);

/// <param name="UnavailableValues">Filter values that no longer apply to the caller, such as a project they cannot see
/// (FR-041); they match nothing.</param>
public sealed record SearchResults(Page<SearchResultRow> Rows, IReadOnlyList<string> UnavailableValues);

/// <param name="Container">The epic or stage, for its tag (Jira parity pass FR-025).</param>
public sealed record SearchResultRow(string Key, WorkItemType Type, string Title, string ProjectKey, string ProjectName,
    StatusOption Status, AssigneeRef? Assignee, IReadOnlyList<string> Labels, DateTimeOffset UpdatedAt, ContainerView? Container = null);

/// <summary>Saved filters (Phase 4 FR-039–FR-041, research R17): only their owner changes them; those shared with a
/// project can be run by its members while the owner can still see it.</summary>
public interface ISavedFilterService
{
    /// <summary>The caller's own filters, then those shared with them.</summary>
    Task<Result<SavedFilterList>> ListAsync(CancellationToken ct);

    /// <summary>The owner's filter, or one shared with the caller; <c>NotFound</c> otherwise.</summary>
    Task<Result<SavedFilterView>> GetAsync(long filterId, CancellationToken ct);

    /// <summary><c>FilterNameTaken</c> for a second filter with the same name.</summary>
    Task<Result<SavedFilterView>> SaveAsync(string name, string query, CancellationToken ct);

    Task<Result<SavedFilterView>> RenameAsync(long filterId, string name, byte[] expectedVersion, CancellationToken ct);

    /// <summary>Replaces the filter's search with the current one.</summary>
    Task<Result<SavedFilterView>> UpdateAsync(long filterId, string query, byte[] expectedVersion, CancellationToken ct);

    Task<Result> DeleteAsync(long filterId, CancellationToken ct);

    /// <summary>Shares with exactly these projects, which the owner must be a member of (any project for an
    /// administrator; <c>NotAMember</c>); none stops sharing.</summary>
    Task<Result<SavedFilterView>> ShareAsync(long filterId, IReadOnlyList<string> projectKeys, CancellationToken ct);
}

public sealed record SavedFilterView(long Id, string Name, string Query, string OwnerName, bool IsMine,
    IReadOnlyList<ProjectRef> SharedWith, byte[] Version);

public sealed record SavedFilterList(IReadOnlyList<SavedFilterView> Mine, IReadOnlyList<SavedFilterView> SharedWithMe);
