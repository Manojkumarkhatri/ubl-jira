using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Work;

/// <summary>Descriptions in formatted text (Phase 4 FR-001–FR-004, FR-007–FR-010, research R3, R4, R6).</summary>
public sealed class FormattedDescriptionTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    private User _bilal = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _bilal = await MemberAsync("bilal");
        ActAs(Owner);
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    private Task<Result<WorkItemDetails>> DescribeAsync(string document, byte[]? version = null) =>
        EditAsync("WEB-1", new WorkItemEdit.Description(document), version);

    private Task<List<(Guid Recipient, NotificationKind Kind)>> NotificationsAsync() =>
        QueryAsync(async db => (await db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToListAsync(Ct))
            .ConvertAll(n => (n.RecipientId, n.Kind)));

    [Fact]
    public async Task P4_US1_AS2_A_description_keeps_headings_lists_code_and_tables_and_history_holds_plain_text()
    {
        var document = Of(
            Heading(2, "Launch plan"),
            Node("bulletList", Node("listItem", Paragraph(Run("Book the venue", "bold")))),
            Node("codeBlock", Run("deploy --prod")),
            Table(["Step", "Owner"], ["Venue", "Amina"]));

        var saved = (await DescribeAsync(document)).ValueOrThrow();

        Assert.Equal(TextFormat.Document, saved.Description!.Format);
        Assert.Equal(FormattedText.Parse(document, "Description").Value!.Json, saved.Description.Content);
        Assert.Equal("Launch plan\nBook the venue\ndeploy --prod\nStep\nOwner\nVenue\nAmina", saved.Description.PlainText);
        var change = (await HistoryAsync("WEB-1")).Items[^1];
        Assert.Equal((WorkItemField.Description, (string?)null, "Launch plan\nBook the venue\ndeploy --prod\nStep\nOwner\nVenue\nAmina"),
            (change.Field, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task Refused_documents_change_nothing()
    {
        Assert.True((await DescribeAsync(Text("Kept"))).IsSuccess);
        var before = await RequireDetailsAsync("WEB-1");

        var unsupported = await DescribeAsync("""{"type":"doc","content":[{"type":"iframe"}]}""");
        var badLink = await DescribeAsync(Of(Paragraph(Link("click", "javascript:alert(1)"))));
        var tooLong = await DescribeAsync(Text(new string('x', 32_001)));

        Assert.Equal(("UnsupportedFormatting", ErrorKind.Validation), (unsupported.Error!.Code, unsupported.Error.Kind));
        Assert.Equal("InvalidLink", badLink.Error!.Code);
        Assert.Equal("TextTooLong", tooLong.Error!.Code);
        var after = await RequireDetailsAsync("WEB-1");
        Assert.Equal("Kept", after.Description!.PlainText);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task A_stale_version_returns_the_current_state()
    {
        var loaded = await RequireDetailsAsync("WEB-1");
        await DescribeAsync(Text("Bilal's version"), loaded.Version);

        var mine = await DescribeAsync(Text("Amina's version"), loaded.Version);

        Assert.Equal(ErrorKind.Conflict, mine.Error!.Kind);
        Assert.Equal("Bilal's version", (await RequireDetailsAsync("WEB-1")).Description!.PlainText);
    }

    [Fact]
    public async Task P4_FR008_Mentions_of_people_outside_the_project_become_plain_text()
    {
        var nora = await Data.UserAsync("nora");

        var saved = (await DescribeAsync(Mentioning("Ask ", _bilal, nora))).ValueOrThrow();

        Assert.Contains(_bilal.Id.ToString(), saved.Description!.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(nora.Id.ToString(), saved.Description.Content, StringComparison.Ordinal);
        Assert.Equal("Ask @Bilal Tester@Nora Tester", saved.Description.PlainText);
        Assert.Equal([(_bilal.Id, NotificationKind.Mentioned)], await NotificationsAsync());
    }

    [Fact]
    public async Task P4_US1_AS7_Saving_again_notifies_only_people_mentioned_for_the_first_time()
    {
        var omar = await MemberAsync("omar");
        ActAs(Owner);
        await DescribeAsync(Mentioning("Ask ", _bilal));

        await DescribeAsync(Mentioning("Ask again ", _bilal, omar));

        Assert.Equal([(_bilal.Id, NotificationKind.Mentioned), (omar.Id, NotificationKind.Mentioned)], await NotificationsAsync());
    }

    [Fact]
    public async Task P4_FR010_A_mention_shows_the_persons_current_name()
    {
        await DescribeAsync(Mentioning("Ask ", _bilal));
        await using (var scope = Harness.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            (await db.Users.SingleAsync(u => u.Id == _bilal.Id, Ct)).DisplayName = "Bilal Khan";
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal("Ask @Bilal Khan", (await RequireDetailsAsync("WEB-1")).Description!.PlainText);
    }

    [Fact]
    public async Task P4_US1_AS6_Mentioning_oneself_notifies_no_one()
    {
        await DescribeAsync(Mentioning("Note to ", Owner));

        Assert.Empty(await NotificationsAsync());
    }

    [Fact]
    public async Task The_List_views_word_filter_reads_the_text_never_the_document()
    {
        await DescribeAsync(Of(Paragraph(Run("Launch", "bold"))));

        var byText = await ListAsync("launch");
        var byJson = await ListAsync("paragraph");

        Assert.Equal(["WEB-1"], byText);
        Assert.Empty(byJson);
    }

    private async Task<IReadOnlyList<string>> ListAsync(string words) =>
        (await CallAsync<IWorkItemListService, Result<WorkItemListView>>(s =>
            s.ListAsync("WEB", new WorkItemListQuery(Text: words), new DateOnly(2026, 9, 27), Ct)))
        .ValueOrThrow().Rows.Items.Select(r => r.Key).ToList();
}
