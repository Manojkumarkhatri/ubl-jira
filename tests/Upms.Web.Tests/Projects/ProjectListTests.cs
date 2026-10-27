using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;
using Upms.Domain.Projects;
using Upms.Web.Components.Pages.Projects;

namespace Upms.Web.Tests.Projects;

/// <summary>The project list (Phase 2 FR-015) shows each project's template (Phase 3 FR-006).</summary>
public sealed class ProjectListTests : BunitTestBase
{
    public ProjectListTests() => Services.AddSingleton<IProjectService>(new ListedProjects());

    [Fact]
    public void P3_FR006_Each_project_shows_its_template()
    {
        var cut = Render<ProjectList>();

        Assert.Contains("Template", cut.FindAll("thead th").Select(th => th.TextContent.Trim()));
        Assert.Equal(["Stage-gate", "Scrum", "Kanban"], cut.FindAll("[data-testid=project-template]").Select(td => td.TextContent.Trim()));
    }

    private sealed class ListedProjects : IProjectService
    {
        public Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct) =>
            Task.FromResult(Result<Page<ProjectSummary>>.Ok(new Page<ProjectSummary>(
            [
                new ProjectSummary("CBU", "Core Banking Upgrade", ProjectRole.ProjectAdmin, false, 4, ProjectTemplate.StageGate),
                new ProjectSummary("MOB", "Mobile App", ProjectRole.Member, false, 2, ProjectTemplate.Scrum),
                new ProjectSummary("WEB", "Website Revamp", ProjectRole.Viewer, false, 7, ProjectTemplate.Kanban),
            ], 3, 1, 50)));

        public Task<string> SuggestKeyAsync(string projectName, CancellationToken ct) => Task.FromResult("");

        public Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, Upms.Domain.Common.WorkTypeSet workTypes, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
