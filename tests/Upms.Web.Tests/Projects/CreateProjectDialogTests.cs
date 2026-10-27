using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;
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
        cut.Find("[data-testid=use-template]").Click();

        cut.Find("#space-name").Input("Branch Network"); // the suggestion is still loading
        cut.Find("#project-key").Input("BRN");
        cut.InvokeAsync(() => _projects.Pending.SetResult("BN"));

        Assert.Equal("BRN", cut.Find("#project-key").GetAttribute("value"));
    }

    [Fact]
    public void A_suggestion_arriving_after_the_user_moves_into_the_key_box_is_not_written_into_it()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);
        cut.Find("[data-testid=use-template]").Click();

        cut.Find("#space-name").Input("Keyboard Drawer"); // the suggestion is still loading
        cut.Find("#project-key").Focus();
        cut.InvokeAsync(() => _projects.Pending.SetResult("KD"));

        Assert.Equal("", cut.Find("#project-key").GetAttribute("value"));

        // Back in the name box, suggestions resume.
        cut.Find("#project-key").Blur();
        cut.Find("#space-name").Input("Keyboard Drawers");
        cut.InvokeAsync(() => _projects.Pending.SetResult("KD"));
        Assert.Equal("KD", cut.Find("#project-key").GetAttribute("value"));
    }

    [Fact]
    public void An_older_suggestion_never_replaces_a_newer_one()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);
        cut.Find("[data-testid=use-template]").Click();

        cut.Find("#space-name").Input("Br");
        var older = _projects.Pending;
        cut.Find("#space-name").Input("Branch Network");
        cut.InvokeAsync(() => _projects.Pending.SetResult("BN"));
        cut.InvokeAsync(() => older.SetResult("BR"));

        Assert.Equal("BN", cut.Find("#project-key").GetAttribute("value"));
    }

    [Fact]
    public void JP_US3_AS1_The_gallery_offers_four_templates_with_Kanban_chosen()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);

        var options = cut.FindAll("fieldset.template-choice input[type=radio]");
        Assert.Equal(["Kanban", "Scrum", "StageGate", "Blank"], options.Select(o => o.GetAttribute("value")));
        Assert.True(options[0].HasAttribute("checked"));
        Assert.All(options, o => Assert.Equal("space-template", o.GetAttribute("name")));
        Assert.All(options, o => Assert.False(string.IsNullOrWhiteSpace(cut.Find($"#{o.GetAttribute("aria-describedby")}").TextContent)));
        Assert.Equal("Choose a template", cut.Find("fieldset.template-choice legend").TextContent.Trim());
    }

    [Fact]
    public void JP_US3_AS2_Choosing_a_template_shows_its_columns_work_types_and_views()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);

        cut.Find("#space-template-Scrum").Change("Scrum");

        var preview = cut.Find("[data-testid=template-preview]");
        Assert.Equal("Scrum", preview.QuerySelector("h3")!.TextContent);
        Assert.Equal(["To Do", "In Progress", "In Review", "Done"], preview.QuerySelectorAll(".preview-columns li span:first-child").Select(s => s.TextContent));
        Assert.Contains("Backlog", preview.TextContent, StringComparison.Ordinal);
        Assert.Equal("polite", preview.GetAttribute("aria-live"));
    }

    [Fact]
    public void JP_US3_AS3_The_chosen_template_is_sent_with_the_space_and_Back_keeps_the_choices()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);
        cut.Find("#space-template-Scrum").Change("Scrum");
        cut.Find("[data-testid=use-template]").Click();
        cut.Find("#space-name").Input("Mobile App");
        cut.Find("#project-key").Input("MOB");

        cut.Find("[data-testid=template-back]").Click();
        Assert.True(cut.Find("#space-template-Scrum").HasAttribute("checked"));
        cut.Find("[data-testid=use-template]").Click();
        Assert.Equal("Mobile App", cut.Find("#space-name").GetAttribute("value"));
        cut.Find("#create-project-form").Submit();

        Assert.Equal(("Mobile App", "MOB", ProjectTemplate.Scrum), Assert.Single(_projects.Created));
    }

    [Fact]
    public void JP_US3_AS4_A_blank_space_gets_the_columns_and_work_types_chosen()
    {
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);
        cut.Find("#space-template-Blank").Change("Blank");
        cut.Find("[data-testid=use-template]").Click();
        cut.Find("#space-name").Input("Data Platform");
        cut.Find("#project-key").Input("DATA");

        Assert.Equal(["To do", "In progress", "Done"], cut.FindAll("[data-testid=blank-column] input").Select(i => i.GetAttribute("value")));
        cut.Find("#blank-column-1-name").Input("Building");
        cut.Find("[data-testid=blank-add-column]").Click();
        cut.Find("#blank-column-2-name").Input("Validating");
        cut.Find("#blank-type-Story").Change(false);
        cut.Find("#create-project-form").Submit();

        var (name, key, setup) = Assert.Single(_projects.CreatedBlank);
        Assert.Equal(("Data Platform", "DATA"), (name, key));
        Assert.Equal(
            [new NewColumn("To do", StatusCategory.ToDo), new NewColumn("Building", StatusCategory.InProgress),
             new NewColumn("Validating", StatusCategory.InProgress), new NewColumn("Done", StatusCategory.Done)],
            setup.Columns);
        Assert.Equal(WorkTypeSet.Epic | WorkTypeSet.Task | WorkTypeSet.Bug, setup.WorkTypes);
        Assert.Empty(_projects.Created);
    }

    [Fact]
    public void Blank_columns_move_and_go_and_refusals_show_beside_them()
    {
        _projects.NextResult = AppError.Rule(ErrorCodes.NoStandardType, "Choose at least one of Story, Task or Bug.");
        var cut = Render<CreateProjectDialog>();
        cut.InvokeAsync(cut.Instance.Open);
        cut.Find("#space-template-Blank").Change("Blank");
        cut.Find("[data-testid=use-template]").Click();

        cut.FindAll("[data-testid=blank-column]")[2].QuerySelectorAll("button").Single(b => b.TextContent == "Move up").Click();
        cut.FindAll("[data-testid=blank-column]")[0].QuerySelectorAll("button").Single(b => b.TextContent == "Remove").Click();
        Assert.Equal(["Done", "In progress"], cut.FindAll("[data-testid=blank-column] input").Select(i => i.GetAttribute("value")));

        cut.Find("#create-project-form").Submit();
        Assert.Equal("Choose at least one of Story, Task or Bug.", cut.Find("#blank-types-error").TextContent);
    }

    private sealed class SlowSuggestions : IProjectService
    {
        public TaskCompletionSource<string> Pending { get; private set; } = new();

        public List<(string Name, string Key, ProjectTemplate Template)> Created { get; } = [];

        public Task<string> SuggestKeyAsync(string projectName, CancellationToken ct)
        {
            Pending = new TaskCompletionSource<string>();
            return Pending.Task;
        }

        public Task<Result<Page<ProjectSummary>>> ListAsync(PageRequest page, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<string>> CreateAsync(string name, string key, string? description, ProjectTemplate template, CancellationToken ct)
        {
            Created.Add((name, key, template));
            return Task.FromResult(Result<string>.Ok(key));
        }

        public List<(string Name, string Key, SpaceSetup Setup)> CreatedBlank { get; } = [];

        public Result<string>? NextResult { get; set; }

        public Task<Result<string>> CreateBlankAsync(string name, string key, string? description, SpaceSetup setup, CancellationToken ct)
        {
            CreatedBlank.Add((name, key, setup));
            return Task.FromResult(NextResult ?? Result<string>.Ok(key));
        }

        public Task<Result<ProjectDetails>> SetWorkTypesAsync(string projectKey, Upms.Domain.Common.WorkTypeSet workTypes, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> SwitchTemplateAsync(string projectKey, ProjectTemplate target, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> GetAsync(string projectKey, CancellationToken ct) => throw new NotSupportedException();

        public Task<Result<ProjectDetails>> UpdateDetailsAsync(string projectKey, string name, string? description, int expectedDetailsVersion,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
