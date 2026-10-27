using Upms.Application.Common.Results;
using Upms.Application.Work;

namespace Upms.Web.Tests.Fakes;

public sealed class FakeMentionService : IMentionService
{
    public List<(string Key, string Term)> Calls { get; } = [];

    public List<PersonView> People { get; } =
    [
        new(Guid.Parse("0000000b-0000-0000-0000-000000000000"), "Bilal Ahmed", "bilal"),
        new(Guid.Parse("0000000c-0000-0000-0000-000000000000"), "Omar Ali", "omar"),
    ];

    public Task<Result<IReadOnlyList<PersonView>>> SuggestAsync(string workItemKey, string term, CancellationToken ct)
    {
        Calls.Add((workItemKey, term));
        IReadOnlyList<PersonView> found = People
            .Where(p => p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) || p.UserName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<PersonView>>.Ok(found));
    }

    public Task<Result<IReadOnlyList<PersonView>>> SuggestInSpaceAsync(string spaceKey, string term, CancellationToken ct) =>
        SuggestAsync(spaceKey, term, ct);
}
