using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>Team rules enforced by the project (Phase 2 FR-007, FR-010, FR-013; data-model.md "Team rules").</summary>
public sealed class ProjectTeamTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owen = Guid.NewGuid();
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly Guid Bilal = Guid.NewGuid();

    private static Project NewProject() => Project.Create("Website Revamp", "WEB", null, Owen, Now).Value!;

    private static ProjectMember Member(Project project, Guid userId) => project.Members.Single(m => m.UserId == userId);

    private static HashSet<Guid> Active(params Guid[] userIds) => [.. userIds];

    [Fact]
    public void P2_US1_AS1_The_creator_is_the_first_and_only_member_as_Project_Admin()
    {
        var project = NewProject();

        var member = Assert.Single(project.Members);
        Assert.Equal(Owen, member.UserId);
        Assert.Equal(ProjectRole.ProjectAdmin, member.Role);
        Assert.Equal(Owen, member.AddedById);
        Assert.Equal(Now, member.AddedAt);
        Assert.Equal(1, project.MembersVersion);
    }

    [Fact]
    public void P2_US1_AS2_Members_are_added_with_their_role_and_each_change_increments_the_version()
    {
        var project = NewProject();

        var amina = project.AddMember(Amina, ProjectRole.Member, Owen, Now);
        var bilal = project.AddMember(Bilal, ProjectRole.Viewer, Owen, Now);

        Assert.True(amina.IsSuccess);
        Assert.True(bilal.IsSuccess);
        Assert.Equal(ProjectRole.Member, Member(project, Amina).Role);
        Assert.Equal(ProjectRole.Viewer, Member(project, Bilal).Role);
        Assert.Equal(3, project.MembersVersion);
    }

    [Fact]
    public void Adding_someone_who_is_already_a_member_is_refused_and_names_their_role()
    {
        var project = NewProject();
        project.AddMember(Bilal, ProjectRole.Viewer, Owen, Now);

        var again = project.AddMember(Bilal, ProjectRole.Member, Owen, Now);

        Assert.Equal(Project.DuplicateMemberCode, again.Error!.Code);
        Assert.Contains("Viewer", again.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, project.Members.Count);
        Assert.Equal(2, project.MembersVersion);
    }

    [Fact]
    public void Roles_can_change_and_members_can_be_removed()
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.Member, Owen, Now);
        project.AddMember(Bilal, ProjectRole.Viewer, Owen, Now);

        Assert.Null(project.ChangeMemberRole(Member(project, Bilal), ProjectRole.Member, Active(Owen, Amina, Bilal), Now));
        Assert.Null(project.RemoveMember(Member(project, Amina), Active(Owen, Amina, Bilal), Now));

        Assert.Equal(ProjectRole.Member, Member(project, Bilal).Role);
        Assert.DoesNotContain(project.Members, m => m.UserId == Amina);
        Assert.Equal(5, project.MembersVersion);
    }

    [Fact]
    public void Giving_someone_the_role_they_already_have_changes_nothing()
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.Member, Owen, Now);

        Assert.Null(project.ChangeMemberRole(Member(project, Amina), ProjectRole.Member, Active(Owen, Amina), Now));

        Assert.Equal(2, project.MembersVersion);
    }

    [Theory]
    [InlineData(ProjectRole.Member)]
    [InlineData(ProjectRole.Viewer)]
    public void P2_US1_AS7_The_last_active_Project_Admin_cannot_take_another_role(ProjectRole role)
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.Member, Owen, Now);

        var error = project.ChangeMemberRole(Member(project, Owen), role, Active(Owen, Amina), Now);

        Assert.Equal(Project.LastProjectAdminCode, error!.Code);
        Assert.Equal(ProjectRole.ProjectAdmin, Member(project, Owen).Role);
        Assert.Equal(2, project.MembersVersion);
    }

    [Fact]
    public void P2_US1_AS7_The_last_active_Project_Admin_cannot_be_removed()
    {
        var project = NewProject();

        var error = project.RemoveMember(Member(project, Owen), Active(Owen), Now);

        Assert.Equal(Project.LastProjectAdminCode, error!.Code);
        Assert.Single(project.Members);
    }

    [Fact]
    public void A_Project_Admin_can_step_down_when_another_active_Project_Admin_remains()
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.ProjectAdmin, Owen, Now);

        Assert.Null(project.ChangeMemberRole(Member(project, Owen), ProjectRole.Member, Active(Owen, Amina), Now));
        Assert.Equal(Project.LastProjectAdminCode,
            project.RemoveMember(Member(project, Amina), Active(Owen, Amina), Now)!.Code);
    }

    [Fact]
    public void A_deactivated_Project_Admin_does_not_count_as_the_one_that_remains()
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.ProjectAdmin, Owen, Now);

        // Amina's account is deactivated: Owen is the only active Project Admin.
        var error = project.RemoveMember(Member(project, Owen), Active(Owen), Now);

        Assert.Equal(Project.LastProjectAdminCode, error!.Code);
    }

    [Fact]
    public void Inactive_Project_Admins_can_always_be_removed_or_retyped_so_administrators_can_repair_a_project()
    {
        var project = NewProject();
        project.AddMember(Amina, ProjectRole.ProjectAdmin, Owen, Now);

        // Every Project Admin is deactivated: changing them never lowers the number of active Project Admins.
        Assert.Null(project.ChangeMemberRole(Member(project, Amina), ProjectRole.Viewer, Active(), Now));
        Assert.Null(project.RemoveMember(Member(project, Owen), Active(), Now));
        Assert.True(project.AddMember(Bilal, ProjectRole.ProjectAdmin, Guid.NewGuid(), Now).IsSuccess);
    }

    [Fact]
    public void Members_of_another_project_are_refused()
    {
        var project = NewProject();
        var other = NewProject();

        Assert.Throws<InvalidOperationException>(() =>
            project.RemoveMember(Member(other, Owen), Active(Owen), Now));
    }
}
