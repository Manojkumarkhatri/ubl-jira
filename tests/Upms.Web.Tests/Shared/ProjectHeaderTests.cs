using Bunit;
using Microsoft.AspNetCore.Components;
using Upms.Domain.Common;
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

    /// <summary>The view tabs; the "More" list that app.js fills with tabs that do not fit repeats them.</summary>
    private static List<string> Tabs(IRenderedComponent<ProjectHeader> cut) =>
        cut.FindAll("nav.project-nav > a").Select(a => a.TextContent.Trim()).ToList();

    [Fact]
    public void The_breadcrumb_leads_back_to_the_spaces_and_the_heading_is_the_title()
    {
        var cut = Show(ProjectPage.Board);

        Assert.Equal("Spaces", cut.Find(".crumbs").TextContent.Trim());
        Assert.Equal("spaces", cut.Find(".crumbs a").GetAttribute("href"));
        Assert.Equal("Website Revamp", cut.Find("h1").TextContent);
    }

    [Fact]
    public void Away_from_the_board_the_Board_tab_leads_back_to_it()
    {
        var cut = Show(ProjectPage.Settings, title: "Space settings");

        Assert.Equal(["spaces"], cut.FindAll(".crumbs a").Select(a => a.GetAttribute("href")));
        Assert.Null(cut.Find("nav.project-nav > a[href='spaces/WEB/board']").GetAttribute("aria-current"));
        Assert.Equal("Space settings", cut.Find("h1").TextContent);
    }

    [Fact]
    public void The_current_page_is_marked_for_assistive_technology()
    {
        var cut = Show(ProjectPage.Board);

        var board = cut.Find("nav.project-nav a[href='spaces/WEB/board']");
        Assert.Equal("page", board.GetAttribute("aria-current"));
        Assert.Equal("Space", cut.Find("nav.project-nav").GetAttribute("aria-label"));
    }

    [Fact]
    public void P2_Every_member_can_switch_between_the_board_the_list_and_the_timeline()
    {
        var cut = Show(ProjectPage.List);

        // A space that has not chosen its tabs shows every view of its template, in Jira's order.
        Assert.Equal(["Summary", "Board", "List", "Calendar", "Timeline", "Approvals", "Forms", "Docs", "Attachments", "Reports", "Archived work items"],
            Tabs(cut));
        Assert.Equal("page", cut.Find("nav.project-nav > a[href='spaces/WEB/list']").GetAttribute("aria-current"));
        Assert.Null(cut.Find("nav.project-nav > a[href='spaces/WEB/board']").GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban, "Kanban")]
    [InlineData(ProjectTemplate.Scrum, "Scrum")]
    [InlineData(ProjectTemplate.StageGate, "Stage-gate")]
    public void P3_The_template_is_named_next_to_the_project_name(ProjectTemplate template, string name)
    {
        var cut = Render<ProjectHeader>(p => p
            .Add(x => x.ProjectKey, "WEB")
            .Add(x => x.Title, "Website Revamp")
            .Add(x => x.Template, template));

        // As in Jira the name stands alone on screen; screen readers hear the template right after it.
        Assert.Equal($", {name} space", cut.Find(".space-title-row h1 + .sr-only").TextContent);
        Assert.Equal("Website Revamp", cut.Find("h1").TextContent);
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban)]
    [InlineData(ProjectTemplate.Scrum)]
    [InlineData(ProjectTemplate.StageGate)]
    public void P3_Every_template_has_the_board_list_and_timeline(ProjectTemplate template)
    {
        var cut = Render<ProjectHeader>(p => p
            .Add(x => x.ProjectKey, "WEB")
            .Add(x => x.Title, "Website Revamp")
            .Add(x => x.Template, template)
            .Add(x => x.Current, ProjectPage.Timeline));

        var views = Tabs(cut);
        Assert.Equal(["Board", "List", "Timeline"], views.Where(v => v is "Board" or "List" or "Timeline"));
        Assert.Equal("page", cut.Find("nav.project-nav > a[href='spaces/WEB/timeline']").GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData(ProjectTemplate.Kanban, new[] { "Summary", "Board", "List", "Timeline" })]
    [InlineData(ProjectTemplate.Scrum, new[] { "Summary", "Backlog", "Board", "List", "Timeline" })]
    [InlineData(ProjectTemplate.StageGate, new[] { "Summary", "Board", "List", "Timeline", "Stages" })]
    public void P3_US2_Only_Scrum_projects_have_a_backlog_tab_and_it_comes_first(ProjectTemplate template, string[] tabs)
    {
        var cut = Render<ProjectHeader>(p => p
            .Add(x => x.ProjectKey, "MOB")
            .Add(x => x.Title, "Mobile App")
            .Add(x => x.Template, template)
            .Add(x => x.Current, ProjectPage.Backlog));

        // The planning views among the tabs (the others, such as Calendar and Docs, every template has).
        Assert.Equal(tabs, Tabs(cut).Where(v => v is "Summary" or "Backlog" or "Board" or "List" or "Timeline" or "Stages"));
        if (template == ProjectTemplate.Scrum)
        {
            Assert.Equal("page", cut.Find("nav.project-nav a[href='spaces/MOB/backlog']").GetAttribute("aria-current"));
        }
    }

    [Fact]
    public void P3_US3_Stage_gate_projects_have_a_stages_tab()
    {
        var cut = Render<ProjectHeader>(p => p
            .Add(x => x.ProjectKey, "CBU")
            .Add(x => x.Title, "Core Banking Upgrade")
            .Add(x => x.Template, ProjectTemplate.StageGate)
            .Add(x => x.Current, ProjectPage.Stages));

        Assert.Equal("page", cut.Find("nav.project-nav a[href='spaces/CBU/stages']").GetAttribute("aria-current"));
    }

    [Fact]
    public void Settings_are_offered_only_to_Space_admins_and_archived_work_items_to_everyone()
    {
        var member = Show(ProjectPage.Board);
        member.Find("[data-testid=space-more]").Click();
        Assert.Equal(["Members"], member.FindAll("[role=menu] [role=menuitem]").Select(i => i.TextContent.Trim()));
        Assert.Empty(member.FindAll("a[href='spaces/WEB/settings']"));
        // Everyone in the space sees its archived work items; only administrators restore them (on that page).
        Assert.Single(member.FindAll("nav.project-nav > a[href='spaces/WEB/archived']"));

        var admin = Show(ProjectPage.Board, canManage: true, canRestoreDeleted: true);
        admin.Find("[data-testid=space-more]").Click();
        Assert.Single(admin.FindAll("a[href='spaces/WEB/settings']"));
        Assert.Single(admin.FindAll("nav.project-nav > a[href='spaces/WEB/archived']"));
    }

    [Fact]
    public void Page_specific_actions_are_shown_in_the_header()
    {
        var cut = Show(ProjectPage.Board, actions: builder => builder.AddMarkupContent(0, "<a class=\"btn\" href=\"#x\">Extra</a>"));

        Assert.Equal("Extra", cut.Find(".space-head a[href='#x']").TextContent);
    }

    [Fact]
    public void JP_US4_AS5_The_more_menu_holds_members_and_settings_and_names_the_current_one()
    {
        // The heading names the space, so the line beside the star names the page (a heading that already names it hides it).
        var cut = Show(ProjectPage.Settings, title: "Website", canManage: true, canRestoreDeleted: true);

        var more = cut.Find("[data-testid=space-more]");
        Assert.Equal(("menu", "false", "More space pages"), (more.GetAttribute("aria-haspopup"), more.GetAttribute("aria-expanded"), more.GetAttribute("aria-label")));
        Assert.Equal("· Space settings", cut.Find(".more-current").TextContent.Trim());
        more.Click();

        Assert.Equal("true", cut.Find("[data-testid=space-more]").GetAttribute("aria-expanded"));
        Assert.Equal(["Members", "Space settings"], cut.FindAll("[role=menu] [role=menuitem]").Select(i => i.TextContent.Trim()));
        Assert.Equal("page", cut.Find("[role=menuitem][href='spaces/WEB/settings']").GetAttribute("aria-current"));
        cut.Find("[role=menu]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void JP_US4_AS6_The_star_button_stars_the_space_and_says_so()
    {
        var cut = Show(ProjectPage.Board);

        var star = cut.Find("[data-testid=star-WEB]");
        Assert.Equal(("Star WEB", "false"), (star.GetAttribute("aria-label"), star.GetAttribute("aria-pressed")));
        star.Click();

        Assert.Contains("WEB", SpaceNavigation.Starred);
        Assert.Equal("true", cut.Find("[data-testid=star-WEB]").GetAttribute("aria-pressed"));
        cut.Find("[data-testid=star-WEB]").Click();
        Assert.DoesNotContain("WEB", SpaceNavigation.Starred);
    }

    [Fact]
    public void JP_US4_AS3_Opening_a_space_page_records_one_visit()
    {
        var cut = Show(ProjectPage.List);
        cut.Render();

        Assert.Equal(["WEB"], SpaceNavigation.Visits);
    }
}
