using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;
using Upms.Web.Components.Pages.Settings;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Settings;

/// <summary>Switching a project between Kanban and Scrum from its settings (Phase 3 FR-005).</summary>
public sealed class ProjectSettingsTemplateTests : BunitTestBase
{
    private readonly TemplateProjects _projects = new();

    public ProjectSettingsTemplateTests()
    {
        Services.AddSingleton<IProjectService>(_projects);
        Services.AddSingleton<IBoardColumnService>(new FakeBoardColumnService());
        Services.AddScoped<LiveAnnouncer>();
    }

    private IRenderedComponent<ProjectSettings> RenderSettings() => Render<ProjectSettings>(p => p.Add(x => x.Key, "WEB"));

    [Fact]
    public void P3_US1_AS9_A_Kanban_project_switches_to_Scrum_after_a_confirmation_that_explains_the_effect()
    {
        var cut = RenderSettings();

        Assert.Contains("Kanban", cut.Find("[data-testid=template-current]").TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=switch-template]").Click();

        var dialog = cut.Find("[data-testid=switch-template-dialog]");
        Assert.Contains("backlog", dialog.TextContent, StringComparison.OrdinalIgnoreCase);
        dialog.QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Switch to Scrum").Click();

        Assert.Equal(("WEB", ProjectTemplate.Scrum, 1), Assert.Single(_projects.Switches));
        Assert.Contains("Scrum", cut.Find("[data-testid=template-current]").TextContent, StringComparison.Ordinal);
        Assert.Equal("Switch to Kanban", cut.Find("[data-testid=switch-template]").TextContent.Trim());
    }

    [Fact]
    public void A_refused_switch_says_why()
    {
        _projects.Details = _projects.Details with { Template = ProjectTemplate.Scrum };
        _projects.NextSwitch = AppError.Rule("SprintsOpen", "Complete or delete the space's active and planned sprints before switching it to Kanban.");
        var cut = RenderSettings();

        cut.Find("[data-testid=switch-template]").Click();
        cut.Find("[data-testid=switch-template-dialog]").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Switch to Kanban").Click();

        Assert.Contains("active and planned sprints", cut.Find("[data-testid=template-error]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Scrum", cut.Find("[data-testid=template-current]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Stage_gate_project_offers_no_switch()
    {
        _projects.Details = _projects.Details with { Template = ProjectTemplate.StageGate };

        var cut = RenderSettings();

        Assert.Contains("Stage-gate", cut.Find("[data-testid=template-current]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=switch-template]"));
    }

    private sealed class TemplateProjects : IProjectService
    {
        public ProjectDetails Details { get; set; } = new("WEB", "Website Revamp", null, "Amina Khan", CanManage: true, DetailsVersion: 1,
            new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));

        public List<(string Key, ProjectTemplate Target, int Version)> Switches { get; } = [];

        public AppError? NextSwitch { get; set; }

        public Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct) => Task.FromResult(Result<ProjectDetails>.Ok(Details));

        public Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, Upms.Domain.Common.WorkTypeSet workTypes, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion,
            CancellationToken ct)
        {
            Switches.Add((projectKey, target, expectedDetailsVersion));
            if (NextSwitch is { } refused)
            {
                return Task.FromResult(Result<ProjectDetails>.Fail(refused));
            }

            Details = Details with { Template = target, DetailsVersion = Details.DetailsVersion + 1 };
            return Task.FromResult(Result<ProjectDetails>.Ok(Details));
        }

        public Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> SuggestKeyAsync(string projectName, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
