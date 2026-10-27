using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>"@" suggestions while writing (Phase 4 FR-008, research R6): the item's project's active members, in any
/// role, for people who can write there.</summary>
public sealed class MentionServiceTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    private Task<Result<IReadOnlyList<PersonView>>> SuggestAsync(string term) =>
        CallAsync<IMentionService, Result<IReadOnlyList<PersonView>>>(s => s.SuggestAsync("WEB-1", term, Ct));

    [Fact]
    public async Task P4_US1_AS5_Suggestions_are_active_members_of_any_role_by_name()
    {
        var bilal = await MemberAsync("bilal", ProjectRole.Viewer);
        var bina = await MemberAsync("bina");
        await MemberAsync("dora");
        await Data.UserAsync("bianca");
        ActAs(Owner);

        var byUserName = (await SuggestAsync("bi")).ValueOrThrow();
        var byDisplayName = (await SuggestAsync("Tester")).ValueOrThrow();

        Assert.Equal([(bilal.Id, "Bilal Tester", "bilal"), (bina.Id, "Bina Tester", "bina")],
            byUserName.Select(p => (p.UserId, p.DisplayName, p.UserName)));
        Assert.Equal(4, byDisplayName.Count);
    }

    [Fact]
    public async Task At_most_10_people_are_suggested()
    {
        for (var i = 0; i < 12; i++)
        {
            await MemberAsync($"member{i:00}");
        }

        ActAs(Owner);

        Assert.Equal(10, (await SuggestAsync("member")).ValueOrThrow().Count);
    }

    [Fact]
    public async Task Viewers_cannot_mention_and_non_members_see_nothing()
    {
        ActAs(await MemberAsync("vera", ProjectRole.Viewer));
        Assert.Equal(ErrorKind.Forbidden, (await SuggestAsync("a")).Error!.Kind);

        ActAs(await Data.UserAsync("nora"));
        Assert.Equal(ErrorKind.NotFound, (await SuggestAsync("a")).Error!.Kind);
    }
}
