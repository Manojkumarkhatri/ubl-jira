using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Projects;
using Upms.Domain.Projects;
using Upms.Web.Components.Pages.Members;
using Upms.Web.Components.Shared;
using Upms.Web.Tests.Fakes;

namespace Upms.Web.Tests.Members;

/// <summary>The members screen (Phase 2 FR-008–FR-013).</summary>
public sealed class MembersPageTests : BunitTestBase
{
    private readonly FakeProjectMemberService _team;
    private readonly List<string> _announced = [];

    public MembersPageTests()
    {
        _team = new FakeProjectMemberService(CurrentUser.UserId!.Value);
        Services.AddSingleton<IProjectMemberService>(_team);
        var announcer = new LiveAnnouncer();
        announcer.Announced += _announced.Add;
        Services.AddSingleton(announcer);
        Services.AddScoped<ViewerTimeZone>();
        Services.AddSingleton<Upms.Application.Identity.IAccountService>(new FakeAccountService());
    }

    private IRenderedComponent<MembersPage> RenderPage() => Render<MembersPage>(p => p.Add(x => x.Key, "WEB"));

    private string? LastFocused() =>
        JSInterop.Invocations.LastOrDefault(i => i.Identifier == "upms.focusById").Arguments?[0] as string;

    private static IElement Row(IRenderedComponent<MembersPage> cut, string displayName) =>
        cut.FindAll("[data-testid=member-row]").Single(r => r.QuerySelector("td")!.TextContent.Contains(displayName, StringComparison.Ordinal));

    [Fact]
    public void The_team_is_listed_with_roles_and_marks_for_the_viewer_and_deactivated_accounts()
    {
        var cut = RenderPage();

        Assert.Equal(4, cut.FindAll("[data-testid=member-row]").Count);
        Assert.Contains("(you)", Row(cut, "Owen Tester").TextContent, StringComparison.Ordinal);
        Assert.Contains("Deactivated", Row(cut, "Gone Away").TextContent, StringComparison.Ordinal);
        Assert.Equal("Viewer", Row(cut, "Bilal Ahmed").QuerySelector("select")!.GetAttribute("value"));
        Assert.Equal("Members", cut.Find("h1").TextContent);
        Assert.Equal("page", cut.Find("nav.project-nav a[href='projects/WEB/members']").GetAttribute("aria-current"));
    }

    [Fact]
    public void Members_and_Viewers_see_the_team_without_any_controls()
    {
        _team.CanManage = false;

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("#member-search"));
        Assert.Empty(cut.FindAll("[data-testid=member-row] select"));
        Assert.Empty(cut.FindAll("[data-testid=member-row] button"));
        Assert.Contains("Viewer", Row(cut, "Bilal Ahmed").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void P2_US1_AS2_A_Project_Admin_finds_a_person_by_typing_and_adds_them_with_a_role()
    {
        var cut = RenderPage();
        Assert.NotNull(cut.Find("#add-member").GetAttribute("disabled"));

        cut.Find("#member-search").Input("carl");
        Assert.Equal(["carl"], _team.Searches);
        Assert.Equal(2, cut.FindAll("input[name=person]").Count);
        cut.FindAll("input[name=person]")[0].Change(FakeProjectMemberService.Carla.ToString());
        cut.Find("#member-role").Change(nameof(ProjectRole.Viewer));
        cut.Find("#add-member").Click();

        Assert.Equal(["add carla Viewer v4"], _team.Calls);
        Assert.Contains("Carla Diaz", Row(cut, "Carla Diaz").TextContent, StringComparison.Ordinal);
        Assert.Contains("Carla Diaz added as Viewer.", _announced);
        Assert.Equal("", cut.Find("#member-search").GetAttribute("value"));
        Assert.Empty(cut.FindAll("input[name=person]"));
    }

    [Fact]
    public void Changing_a_role_saves_it_with_the_team_version()
    {
        var cut = RenderPage();

        Row(cut, "Amina Khan").QuerySelector("select")!.Change(nameof(ProjectRole.Viewer));

        Assert.Equal(["role amina Viewer v4"], _team.Calls);
        Assert.Equal("Viewer", Row(cut, "Amina Khan").QuerySelector("select")!.GetAttribute("value"));
        Assert.Contains("Amina Khan is now a Viewer.", _announced);
        Assert.Equal("Role of Amina Khan", Row(cut, "Amina Khan").QuerySelector("select")!.GetAttribute("aria-label"));
        Assert.Equal($"role-{FakeProjectMemberService.Amina:N}", LastFocused());
    }

    [Fact]
    public void Removing_a_member_asks_first()
    {
        var cut = RenderPage();

        Row(cut, "Amina Khan").QuerySelectorAll("button").Single(b => b.TextContent.Trim() == "Remove").Click();
        Assert.Empty(_team.Calls);
        Assert.Contains("Remove Amina Khan from the project?", Row(cut, "Amina Khan").TextContent, StringComparison.Ordinal);
        Assert.Equal("confirm-remove", LastFocused());
        cut.Find("#confirm-remove").Click();

        Assert.Equal(["remove amina v4"], _team.Calls);
        Assert.DoesNotContain(cut.FindAll("[data-testid=member-row]"), r => r.TextContent.Contains("Amina Khan", StringComparison.Ordinal));
        Assert.Contains("Amina Khan removed from the project.", _announced);
        Assert.Equal("member-search", LastFocused());
    }

    [Fact]
    public void P2_US1_AS7_A_refused_change_explains_why_and_keeps_the_saved_role()
    {
        _team.NextResult = () => AppError.Rule(ErrorCodes.LastProjectAdmin,
            "This is the project's last active Project Admin. Make someone else a Project Admin first.");
        var cut = RenderPage();

        Row(cut, "Owen Tester").QuerySelector("select")!.Change(nameof(ProjectRole.Member));

        Assert.Equal("This is the project's last active Project Admin. Make someone else a Project Admin first.",
            cut.Find("[role=alert]").TextContent.Trim());
        Assert.Equal("ProjectAdmin", Row(cut, "Owen Tester").QuerySelector("select")!.GetAttribute("value"));
    }

    [Fact]
    public void A_conflict_shows_the_latest_team()
    {
        var latest = _team.View() with
        {
            MembersVersion = 9,
            Members = [.. _team.Members, new MemberView(Guid.NewGuid(), "Dana Noor", "dana", ProjectRole.Member, true, false, DateTimeOffset.UtcNow)],
        };
        _team.NextResult = () => AppError.Conflict("The team was changed by someone else.", latest);
        var cut = RenderPage();

        Row(cut, "Bilal Ahmed").QuerySelector("select")!.Change(nameof(ProjectRole.Member));

        Assert.Contains("The team was changed by someone else.", cut.Find("[data-testid=conflict]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Dana Noor", Row(cut, "Dana Noor").TextContent, StringComparison.Ordinal);

        Row(cut, "Amina Khan").QuerySelector("select")!.Change(nameof(ProjectRole.Viewer));
        Assert.Equal("role amina Viewer v9", _team.Calls[^1]);
    }
}
