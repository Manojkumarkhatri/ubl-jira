using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>Role names as people read them (Jira parity pass FR-019; research R1).</summary>
public sealed class RoleNamesTests
{
    [Theory]
    [InlineData(ProjectRole.ProjectAdmin, "Manages the space")]
    [InlineData(ProjectRole.Member, "Works on items")]
    [InlineData(ProjectRole.Viewer, "Looks only")]
    public void JP_FR019_Roles_read_as_space_roles(ProjectRole role, string name)
    {
        Assert.Equal(name, role.DisplayName());
    }

    [Theory]
    [InlineData(UserType.PortfolioLead, ProjectRole.ProjectAdmin)]
    [InlineData(UserType.Member, ProjectRole.Member)]
    [InlineData(UserType.ReadOnly, ProjectRole.Viewer)]
    public void A_person_s_default_ticks_give_their_role_in_the_spaces_they_are_in(UserType type, ProjectRole role)
    {
        Assert.Equal(role, ProjectRoleNames.For(RightsCatalog.Defaults(type)));
    }

    [Fact]
    public void Own_ticks_add_to_and_take_from_the_type_but_an_Admin_keeps_every_tick()
    {
        var member = RightsCatalog.Effective(UserType.Member, RightsCatalog.Defaults(UserType.Member), Rights.DeleteAnyWork, Rights.Export);
        Assert.True(member.Has(Rights.DeleteAnyWork));
        Assert.False(member.Has(Rights.Export));
        Assert.True(member.Has(Rights.EditWork));

        // A type's limit wins over a person's own ticks: a Member never adds Portfolio leads or creates spaces (owner, 2026-10-26).
        var locked = RightsCatalog.Effective(UserType.Member, RightsCatalog.Defaults(UserType.Member), Rights.AddPortfolioLeads | Rights.CreateSpaces, Rights.None);
        Assert.False(locked.Has(Rights.AddPortfolioLeads));
        Assert.False(locked.Has(Rights.CreateSpaces));

        Assert.Equal(Rights.All, RightsCatalog.Effective(UserType.Admin, Rights.None, Rights.None, Rights.All));
    }
}
