using Upms.Application.Common.Results;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Web.Tests.Fakes;

/// <summary>Records sprint changes for component tests; each succeeds unless a test sets the next result.</summary>
public sealed class FakeSprintService : ISprintService
{
    public int Created { get; private set; }

    public List<(long Id, string Name, string? Goal)> Updates { get; } = [];

    public List<(long Id, DateOnly Start, DateOnly? End, string? Goal)> Starts { get; } = [];

    public List<(long Id, long? MoveTo)> Completions { get; } = [];

    public List<long> Deletes { get; } = [];

    /// <summary>The result of the next start, complete, update or create call instead of success.</summary>
    public AppError? NextError { get; set; }

    public Dictionary<int, SprintReport> Reports { get; } = [];

    public Task<Result<SprintSection>> CreateAsync(string projectKey, CancellationToken ct)
    {
        Created++;
        return Answer(new SprintSection(20 + Created, 2 + Created, $"MOB Sprint {2 + Created}", null, SprintState.Planned, null, null,
            new SprintTotals(0, 0, 0, 0, 0), [], [1]));
    }

    public Task<Result<SprintSection>> UpdateAsync(long sprintId, string name, string? goal, byte[] expectedVersion, CancellationToken ct)
    {
        Updates.Add((sprintId, name, goal));
        return Answer(Section(sprintId, name, SprintState.Planned));
    }

    public Task<Result<SprintSection>> StartAsync(long sprintId, DateOnly start, DateOnly? end, string? goal, byte[] expectedVersion,
        CancellationToken ct)
    {
        Starts.Add((sprintId, start, end, goal));
        return Answer(Section(sprintId, "MOB Sprint 2", SprintState.Active));
    }

    public Task<Result<SprintSection>> CompleteAsync(long sprintId, long? moveOpenItemsToSprintId, byte[] expectedVersion,
        CancellationToken ct)
    {
        Completions.Add((sprintId, moveOpenItemsToSprintId));
        return Answer(Section(sprintId, "MOB Sprint 1", SprintState.Completed));
    }

    public Task<Result> DeleteAsync(long sprintId, byte[] expectedVersion, CancellationToken ct)
    {
        Deletes.Add(sprintId);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<SprintReport>> GetReportAsync(string projectKey, int sprintNumber, CancellationToken ct) =>
        Task.FromResult(Reports.TryGetValue(sprintNumber, out var report)
            ? Result<SprintReport>.Ok(report)
            : Result<SprintReport>.Fail(AppError.NotFound("sprint report")));

    private static SprintSection Section(long id, string name, SprintState state) =>
        new(id, 1, name, null, state, null, null, new SprintTotals(0, 0, 0, 0, 0), [], [1]);

    private Task<Result<SprintSection>> Answer(SprintSection section)
    {
        var error = NextError;
        NextError = null;
        return Task.FromResult(error is null ? Result<SprintSection>.Ok(section) : Result<SprintSection>.Fail(error));
    }
}
