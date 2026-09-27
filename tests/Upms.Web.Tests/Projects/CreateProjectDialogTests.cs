using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Web.Components.Pages.Projects;

namespace Upms.Web.Tests.Projects;

/// <summary>The key suggested from the project name (FR-011).</summary>
public sealed class CreateProjectDialogTests : BunitTestBase
{
    private readonly SlowSuggestions _projects = new();

    public CreateProjectDialogTests() => Services.AddSingleton<IProjectService>(_projects);

    [Fact]
    public void A_key_typed_while_the_suggestion_loads_is_kept()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);

        cut.Find("#project-name").Input("Branch Network"); // the suggestion is still loading
        cut.Find("#project-key").Input("BRN");
        cut.InvokeAsync(() => _projects.Pending.SetResult("BN"));

        Assert.Equal("BRN", cut.Find("#project-key").GetAttribute("value"));
    }

    [Fact]
    public void An_older_suggestion_never_replaces_a_newer_one()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);

        cut.Find("#project-name").Input("Br");
        var older = _projects.Pending;
        cut.Find("#project-name").Input("Branch Network");
        cut.InvokeAsync(() => _projects.Pending.SetResult("BN"));
        cut.InvokeAsync(() => older.SetResult("BR"));

        Assert.Equal("BN", cut.Find("#project-key").GetAttribute("value"));
    }

    private sealed class SlowSuggestions : IProjectService
    {
        public TaskCompletionSource<string> Pending { get; private set; } = new();

        public Task<string> SuggestKeyAsync(string projectName, CancellationToken ct)
        {
            Pending = new TaskCompletionSource<string>();
            return Pending.Task;
        }

        public Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<string>> CreateAsync(string name, string key, string? description, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
