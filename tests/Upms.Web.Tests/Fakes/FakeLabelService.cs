using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>Labels kept in memory per item, with labels "in use" to suggest; records every call.</summary>
public sealed class FakeLabelService : ILabelService
{
    public Dictionary<string, List<string>> OnItems { get; } = new(StringComparer.Ordinal);

    public List<string> InUse { get; } = [];

    public List<(string ProjectKey, string Term)> Suggested { get; } = [];

    public Task<Result<IReadOnlyList<string>>> SuggestAsync(string projectKey, string term, CancellationToken ct)
    {
        Suggested.Add((projectKey, term));
        return Task.FromResult(Result<IReadOnlyList<string>>.Ok(
            InUse.Where(l => l.StartsWith(term, StringComparison.OrdinalIgnoreCase)).Take(10).ToList()));
    }

    public Task<Result<IReadOnlyList<string>>> AddAsync(string workItemKey, string label, CancellationToken ct)
    {
        if (Label.Check(label) is { } invalid)
        {
            return Task.FromResult(Result<IReadOnlyList<string>>.Fail(new AppError(ErrorKind.Validation, invalid.Code, invalid.Message,
                new Dictionary<string, string[]> { ["Label"] = [invalid.Message] })));
        }

        var labels = Labels(workItemKey);
        var name = InUse.FirstOrDefault(l => l.Equals(label.Trim(), StringComparison.OrdinalIgnoreCase)) ?? label.Trim();
        if (!labels.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            labels.Add(name);
        }

        return Task.FromResult(Result<IReadOnlyList<string>>.Ok(labels.Order(StringComparer.OrdinalIgnoreCase).ToList()));
    }

    public Task<Result<IReadOnlyList<string>>> RemoveAsync(string workItemKey, string label, CancellationToken ct)
    {
        var labels = Labels(workItemKey);
        labels.RemoveAll(l => l.Equals(label, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(Result<IReadOnlyList<string>>.Ok(labels.Order(StringComparer.OrdinalIgnoreCase).ToList()));
    }

    private List<string> Labels(string workItemKey) =>
        OnItems.TryGetValue(workItemKey, out var labels) ? labels : OnItems[workItemKey] = [];
}
