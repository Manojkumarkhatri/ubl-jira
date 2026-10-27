using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>"@" suggestions while writing a description or comment (Phase 4 FR-008, research R6).</summary>
public interface IMentionService
{
    const int MaxSuggestions = 10;

    /// <summary>Contribute right. Active members of the item's project in any role whose display name or user name
    /// contains <paramref name="term"/>, by name, at most 10.</summary>
    Task<Result<IReadOnlyList<PersonView>>> SuggestAsync(string workItemKey, string term, CancellationToken ct);

    /// <summary>The same, for text written before its item exists: members of the space (the Create form, Jira parity pass
    /// FR-004).</summary>
    Task<Result<IReadOnlyList<PersonView>>> SuggestInSpaceAsync(string spaceKey, string term, CancellationToken ct);
}
