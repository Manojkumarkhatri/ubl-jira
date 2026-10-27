using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Common;
using Upms.Web.Components.Pages.Settings;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Settings;

/// <summary>Work types in space settings (Jira parity pass US3 scenario 6, FR-017).</summary>
public sealed class WorkTypesEditorTests : BunitTestBase
{
    private readonly FakeProjectService _projects = new();

    private static readonly ProjectDetails Details = new("WEB", "Website Revamp", null, "Amina Khan", true, 4, DateTimeOffset.UnixEpoch,
        ProjectTemplate.Kanban, WorkTypeSet.All, new Dictionary<WorkTypeSet, int> { [WorkTypeSet.Bug] = 4, [WorkTypeSet.Task] = 1 });

    public WorkTypesEditorTests()
    {
        Services.AddSingleton<IProjectService>(_projects);
        Services.AddScoped<LiveAnnouncer>();
    }

    [Fact]
    public void JP_US3_AS6_Each_type_shows_how_many_items_use_it_and_saving_sends_the_choice_with_the_version()
    {
        ProjectDetails? saved = null;
        var cut = Render<WorkTypesEditor>(p => p.Add(x => x.Details, Details).Add(x => x.OnSaved, d => saved = d));

        Assert.Equal(["Epic (not used)", "Story (not used)", "Task (1 item)", "Bug (4 items)"],
            cut.FindAll("label.check").Select(l => System.Text.RegularExpressions.Regex.Replace(l.TextContent, @"\s+", " ").Trim()));
        Assert.True(cut.Find("[data-testid=save-work-types]").HasAttribute("disabled"));

        cut.Find("#work-type-Epic").Change(false);
        cut.Find("[data-testid=save-work-types]").Click();

        Assert.Equal(("WEB", WorkTypeSet.Story | WorkTypeSet.Task | WorkTypeSet.Bug, 4), Assert.Single(_projects.WorkTypeSaves));
        Assert.Equal(5, saved!.DetailsVersion);
        Assert.Contains("Saved", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_is_shown_and_the_choice_stays()
    {
        _projects.NextWorkTypesResult = AppError.Rule(ErrorCodes.WorkTypeInUse, "Bugs are still used by 4 items in this space.");
        var cut = Render<WorkTypesEditor>(p => p.Add(x => x.Details, Details));

        cut.Find("#work-type-Bug").Change(false);
        cut.Find("[data-testid=save-work-types]").Click();

        Assert.Equal("Bugs are still used by 4 items in this space.", cut.Find("#work-types-error").TextContent);
        Assert.False(cut.Find("#work-type-Bug").HasAttribute("checked"));
    }
}
