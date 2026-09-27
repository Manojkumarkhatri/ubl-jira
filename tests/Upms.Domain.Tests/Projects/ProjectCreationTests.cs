using Upms.Domain.Common;
using Upms.Domain.Projects;

namespace Upms.Domain.Tests.Projects;

/// <summary>Creating a project (FR-011, FR-012, FR-016).</summary>
public sealed class ProjectCreationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void US1_AS3_A_new_project_has_To_Do_In_Progress_and_Done_and_its_creator_as_owner()
    {
        var project = Project.Create("Website Revamp", "WEB", "The new public site", Owner, Now).Value!;

        Assert.Equal("WEB", project.Key);
        Assert.Equal("Website Revamp", project.Name);
        Assert.Equal("WEBSITE REVAMP", project.NormalizedName);
        Assert.Equal("The new public site", project.Description);
        Assert.Equal(Owner, project.OwnerId);
        Assert.Equal(1, project.NextItemNumber);
        Assert.Equal(1, project.BoardVersion);
        Assert.Collection(project.Statuses,
            s => Assert.Equal(("To Do", StatusCategory.ToDo, 0), (s.Name, s.Category, s.Position)),
            s => Assert.Equal(("In Progress", StatusCategory.InProgress, 1), (s.Name, s.Category, s.Position)),
            s => Assert.Equal(("Done", StatusCategory.Done, 2), (s.Name, s.Category, s.Position)));
    }

    [Theory]
    [InlineData("WE")]
    [InlineData("WEB")]
    [InlineData("W2")]
    [InlineData("ABCDEFGHIJ")]
    public void Valid_keys_are_accepted(string key)
    {
        Assert.True(Project.Create("Name", key, null, Owner, Now).IsSuccess);
    }

    [Theory]
    [InlineData("W")]
    [InlineData("2WEB")]
    [InlineData("WEB-1")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("WÉB")]
    [InlineData("")]
    public void Invalid_keys_are_refused(string key)
    {
        var error = Project.Create("Name", key, null, Owner, Now).Error;

        Assert.NotNull(error);
        Assert.Equal("InvalidProjectKey", error.Code);
        Assert.Equal("Key", error.Field);
    }

    [Fact]
    public void Keys_are_stored_in_upper_case()
    {
        Assert.Equal("WEB", Project.Create("Name", " web ", null, Owner, Now).Value!.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_name_is_required(string name)
    {
        Assert.Equal("Name", Project.Create(name, "WEB", null, Owner, Now).Error!.Field);
    }

    [Fact]
    public void The_name_has_at_most_80_characters()
    {
        Assert.True(Project.Create(new string('n', 80), "WEB", null, Owner, Now).IsSuccess);
        Assert.Equal("Name", Project.Create(new string('n', 81), "WEB", null, Owner, Now).Error!.Field);
    }

    [Fact]
    public void The_description_is_optional_and_has_at_most_2000_characters()
    {
        Assert.Null(Project.Create("Name", "WEB", "   ", Owner, Now).Value!.Description);
        Assert.True(Project.Create("Name", "WEB", new string('d', 2000), Owner, Now).IsSuccess);
        Assert.Equal("Description", Project.Create("Name", "WEB", new string('d', 2001), Owner, Now).Error!.Field);
    }

    [Fact]
    public void Details_can_be_changed_but_the_key_cannot()
    {
        var project = Project.Create("Website Revamp", "WEB", null, Owner, Now).Value!;

        var error = project.UpdateDetails("Website 2.0", "Second phase", Now.AddDays(1));

        Assert.Null(error);
        Assert.Equal("Website 2.0", project.Name);
        Assert.Equal("WEBSITE 2.0", project.NormalizedName);
        Assert.Equal("Second phase", project.Description);
        Assert.Equal(2, project.DetailsVersion);
        Assert.Equal("WEB", project.Key);
        Assert.Null(typeof(Project).GetProperty(nameof(Project.Key))!.GetSetMethod());
    }
}
