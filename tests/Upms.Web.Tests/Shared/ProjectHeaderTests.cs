using Bunit;
using Microsoft.AspNetCore.Components;
using Upms.Web.Components.Shared;

namespace Upms.Web.Tests.Shared;

/// <summary>The header shared by every project page (Phase 2 contracts/ui-routes.md, "Project header").</summary>
public sealed class ProjectHeaderTests : BunitTestBase
{
    private IRenderedComponent<ProjectHeader> Show(ProjectPage current, string title = "Website Revamp", bool canManage = false,
        bool canRestoreDeleted = false, RenderFragment? actions = null) =>
        Render<ProjectHeader>(p => p
            .Add(x => x.ProjectKey, "WEB")
            .Add(x => x.Title, title)
            .Add(x => x.Current, current)
            .Add(x => x.CanManage, canManage)
            .Add(x => x.CanRestoreDeleted, canRestoreDeleted)
            .Add(x => x.ChildContent, actions));

    [Fact]
    public void The_breadcrumb_leads_back_to_the_projects_and_the_heading_is_the_title()
    {
        var cut = Show(ProjectPage.Board);

        Assert.Equal("Projects / WEB", cut.Find(".crumbs").TextContent.Trim());
        Assert.Equal("projects", cut.Find(".crumbs a").GetAttribute("href"));
        Assert.Equal("Website Revamp", cut.Find("h1").TextContent);
    }

    [Fact]
    public void Away_from_the_board_the_key_links_to_the_board()
    {
        var cut = Show(ProjectPage.Settings, title: "Project settings");

        var links = cut.FindAll(".crumbs a");
        Assert.Equal(["projects", "projects/WEB/board"], links.Select(a => a.GetAttribute("href")));
        Assert.Equal("Project settings", cut.Find("h1").TextContent);
    }

    [Fact]
    public void The_current_page_is_marked_for_assistive_technology()
    {
        var cut = Show(ProjectPage.Board);

        var board = cut.Find("nav.project-nav a[href='projects/WEB/board']");
        Assert.Equal("page", board.GetAttribute("aria-current"));
        Assert.Equal("Project", cut.Find("nav.project-nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void P2_Every_member_can_switch_between_the_board_and_the_list()
    {
        var cut = Show(ProjectPage.List);

        Assert.Equal(["Board", "List", "Members"], cut.FindAll("nav.project-nav a").Select(a => a.TextContent.Trim()));
        Assert.Equal("page", cut.Find("nav.project-nav a[href='projects/WEB/list']").GetAttribute("aria-current"));
        Assert.Null(cut.Find("nav.project-nav a[href='projects/WEB/board']").GetAttribute("aria-current"));
    }

    [Fact]
    public void Settings_and_deleted_tasks_are_offered_only_to_those_allowed()
    {
        var member = Show(ProjectPage.Board);
        Assert.Empty(member.FindAll("a[href='projects/WEB/settings']"));
        Assert.Empty(member.FindAll("a[href='projects/WEB/deleted']"));

        var admin = Show(ProjectPage.Board, canManage: true, canRestoreDeleted: true);
        Assert.Single(admin.FindAll("a[href='projects/WEB/settings']"));
        Assert.Single(admin.FindAll("a[href='projects/WEB/deleted']"));
    }

    [Fact]
    public void Page_specific_actions_are_shown_in_the_header()
    {
        var cut = Show(ProjectPage.Board, actions: builder => builder.AddMarkupContent(0, "<a class=\"btn\" href=\"#x\">Extra</a>"));

        Assert.Equal("Extra", cut.Find(".page-head a[href='#x']").TextContent);
    }
}
