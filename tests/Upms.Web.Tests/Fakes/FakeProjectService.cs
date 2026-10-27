using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Web.Tests.Fakes;

/// <summary>The project list for pages that offer projects to choose from; the rest is not used by them.</summary>
public sealed class FakeProjectService : IProjectService
{
    public List<ProjectSummary> Projects { get; } =
    [
        new("WEB", "Website Revamp", ProjectRole.ProjectAdmin, false, 3),
        new("MOB", "Mobile App", ProjectRole.Member, false, 2),
    ];

    public Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct) =>
        Task.FromResult(Result<Page<ProjectSummary>>.Ok(new Page<ProjectSummary>(Projects.ToList(), Projects.Count, 1, 100)));

    public Task<string> SuggestKeyAsync(string projectName, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct) =>
        throw new NotSupportedException();

    public List<(string Key, Upms.Domain.Common.WorkTypeSet Types, int Version)> WorkTypeSaves { get; } = [];

    public Result<ProjectDetails>? NextWorkTypesResult { get; set; }

    public Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, Upms.Domain.Common.WorkTypeSet workTypes, int expectedDetailsVersion,
        CancellationToken ct)
    {
        WorkTypeSaves.Add((projectKey, workTypes, expectedDetailsVersion));
        return Task.FromResult(NextWorkTypesResult ?? Result<ProjectDetails>.Ok(new ProjectDetails(projectKey, "Website Revamp", null, "Amina Khan",
            true, expectedDetailsVersion + 1, DateTimeOffset.UnixEpoch, Upms.Domain.Common.ProjectTemplate.Kanban, workTypes)));
    }

    public Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description, int expectedDetailsVersion,
        CancellationToken ct) => throw new NotSupportedException();
}
