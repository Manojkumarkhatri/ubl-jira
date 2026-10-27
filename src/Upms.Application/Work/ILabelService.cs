using Upms.Application.Common.Results;

namespace Upms.Application.Work;

/// <summary>Labels on work items (Phase 4 FR-042–FR-044, FR-048, research R18). Each answer is the item's labels after the
/// change, sorted by name.</summary>
public interface ILabelService
{
    /// <summary>Up to 10 labels used on live items in projects the caller can see whose name starts with
    /// <paramref name="term"/> (any case); needs the right to contribute to <paramref name="projectKey"/> (FR-044).</summary>
    Task<Result<IReadOnlyList<string>>> SuggestAsync(string projectKey, string term, CancellationToken ct);

    /// <summary>Adds the label, reusing an existing one in any case (FR-043); <c>InvalidLabel</c>, <c>TooManyLabels</c>.
    /// Adding a label the item already has changes nothing.</summary>
    Task<Result<IReadOnlyList<string>>> AddAsync(string workItemKey, string label, CancellationToken ct);

    /// <summary>Removes the label (any case); removing one the item does not have changes nothing.</summary>
    Task<Result<IReadOnlyList<string>>> RemoveAsync(string workItemKey, string label, CancellationToken ct);
}
