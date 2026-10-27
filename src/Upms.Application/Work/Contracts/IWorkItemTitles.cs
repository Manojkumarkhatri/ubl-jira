namespace Upms.Application.Work.Contracts;

/// <summary>Work item keys and titles for other modules: the notification panel shows each item's current key and title
/// (Phase 4 research R8).</summary>
public interface IWorkItemTitles
{
    /// <summary>The items among <paramref name="workItemIds"/>, deleted ones included: notifications about a deleted item
    /// stay listed.</summary>
    Task<IReadOnlyDictionary<long, WorkItemTitle>> GetAsync(IReadOnlyCollection<long> workItemIds, CancellationToken ct);
}

public sealed record WorkItemTitle(long Id, string Key, string Title)
{
    /// <summary>The project key, which starts every item key.</summary>
    public string ProjectKey => Key[..Math.Max(0, Key.LastIndexOf('-'))];
}
