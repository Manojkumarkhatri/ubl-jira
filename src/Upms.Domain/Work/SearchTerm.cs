namespace Upms.Domain.Work;

/// <summary>One word of a work item's title, description or live comments (Phase 4 research R15). A search matches terms
/// that start with each typed word, as an index seek; the project is kept for the access filter.</summary>
public sealed class SearchTerm
{
    private SearchTerm()
    {
    }

    public long WorkItemId { get; private set; }

    public string Term { get; private set; } = "";

    public long ProjectId { get; private set; }

    public static SearchTerm For(long workItemId, long projectId, string term) =>
        new() { WorkItemId = workItemId, ProjectId = projectId, Term = term };
}

/// <summary>When a person last opened an item's drawer (Phase 4 FR-033): the search box lists the latest ones.</summary>
public sealed class RecentView
{
    public const int Kept = 50;

    private RecentView()
    {
    }

    public Guid UserId { get; private set; }

    public long WorkItemId { get; private set; }

    public DateTimeOffset ViewedAt { get; private set; }

    public static RecentView Of(Guid userId, long workItemId, DateTimeOffset at) =>
        new() { UserId = userId, WorkItemId = workItemId, ViewedAt = at };
}

/// <summary>How far the background search indexing has gone (Phase 4 research R15): every item up to
/// <see cref="LastWorkItemId"/> has been looked at. It lives in its own one-row table, so indexing never writes to a work
/// item and never changes the version someone's open drawer holds.</summary>
public sealed class SearchIndexProgress
{
    public const int SingleId = 1;

    private SearchIndexProgress()
    {
    }

    public int Id { get; private set; }

    public long LastWorkItemId { get; private set; }
}
