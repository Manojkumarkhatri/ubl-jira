using Upms.Application.Common.Results;
using Upms.Application.Projects;

namespace Upms.Web.Tests.Fakes;

/// <summary>The space trash: nobody may delete unless a test says so.</summary>
public sealed class FakeSpaceTrashService : ISpaceTrashService
{
    public bool CanDelete { get; set; }

    public List<string> Deleted { get; } = [];

    public Task<bool> CanDeleteAsync(string projectKey, CancellationToken ct) => Task.FromResult(CanDelete);

    public Task<Result> DeleteAsync(string projectKey, CancellationToken ct)
    {
        Deleted.Add(projectKey);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<IReadOnlyList<TrashedSpace>>> ListAsync(CancellationToken ct) =>
        Task.FromResult(Result<IReadOnlyList<TrashedSpace>>.Ok([]));

    public Task<Result> RestoreAsync(string projectKey, CancellationToken ct) => Task.FromResult(Result.Ok());
}
