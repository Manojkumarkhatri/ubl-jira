using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Infrastructure.Persistence;

namespace Upms.Application.Tests.Work;

/// <summary>The Phase 4 upgrade changes nothing visible (Phase 4 FR-004, SC-008, research R22): a database migrated to
/// the last Phase 3 migration and loaded with pilot-style data comes out with every description and comment reading as
/// before, with each item's creator, assignee and commenters watching it, with every item found by search once the search
/// index worker has run, and with every account getting the emails aimed at it.</summary>
public sealed class CollaborationUpgradeTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string LastPhase3Migration = "20260929192440_Dependencies";
    private static readonly DateTimeOffset At = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = $"UpmsUpgrade4_{Guid.NewGuid():N}",
    }.ConnectionString;

    private ServiceHarness _harness = null!;
    private readonly Guid _owen = Guid.NewGuid();
    private readonly Guid _amina = Guid.NewGuid();
    private readonly Guid _bilal = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(LastPhase3Migration, Ct);
        }

        _harness = new ServiceHarness(_connectionString);

        // Phase 3 accounts, written with the Phase 3 schema (no email choice yet).
        foreach (var (id, name) in new[] { (_owen, "owen"), (_amina, "amina"), (_bilal, "bilal") })
        {
            await ExecuteAsync($"""
                INSERT INTO AspNetUsers (Id, DisplayName, OrganizationRole, IsActive, MustChangePassword, CreatedAt, UserName, NormalizedUserName,
                    Email, NormalizedEmail, EmailConfirmed, SecurityStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ('{id}', N'{name} Tester', 'User', 1, 0, '{At:O}', N'{name}', N'{name.ToUpperInvariant()}',
                    N'{name}@example.com', N'{name.ToUpperInvariant()}@EXAMPLE.COM', 1, N'{Guid.NewGuid():N}', 0, 0, 1, 0);
                """);
        }

        // Phase 3 data, written with the Phase 3 schema: WEB-1 (created by Owen, assigned to Amina) with a description of
        // two lines and a link, a comment by Bilal and a deleted comment by Amina; WEB-2 (created by Amina) with a comment
        // by Owen.
        await ExecuteAsync($"""
            INSERT INTO Projects ([Key], Name, NormalizedName, Description, OwnerId, NextItemNumber, BoardVersion, DetailsVersion, MembersVersion, CreatedAt, UpdatedAt, Template)
            VALUES ('WEB', N'Website Revamp', N'WEBSITE REVAMP', NULL, '{_owen}', 3, 1, 1, 1, '{At:O}', '{At:O}', 'Kanban');
            DECLARE @web bigint = (SELECT Id FROM Projects WHERE [Key] = 'WEB');
            INSERT INTO ProjectStatuses (ProjectId, Name, NormalizedName, Category, Position, WipLimit)
            VALUES (@web, N'To Do', N'TO DO', 'ToDo', 0, NULL), (@web, N'Done', N'DONE', 'Done', 1, NULL);
            INSERT INTO ProjectMembers (ProjectId, UserId, Role, AddedAt, AddedById)
            VALUES (@web, '{_owen}', 'ProjectAdmin', '{At:O}', NULL), (@web, '{_amina}', 'Member', '{At:O}', '{_owen}'),
                   (@web, '{_bilal}', 'Member', '{At:O}', '{_owen}');
            DECLARE @todo bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web AND Name = N'To Do');
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, Priority, StatusId, [Rank],
                CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, DeletedAt, DeletedById, AssigneeId, StartDate, DueDate)
            VALUES (@web, 1, 'WEB-1', 'Task', NULL, N'Plan the launch', N'Line one' + CHAR(10) + N'See https://example.com/plan', 'High', @todo, 'a0', '{_owen}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL, '{_amina}', NULL, NULL),
                   (@web, 2, 'WEB-2', 'Task', NULL, N'Book the venue', NULL, 'Medium', @todo, 'a1', '{_amina}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL, NULL, NULL, NULL);
            DECLARE @web1 bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-1');
            DECLARE @web2 bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-2');
            INSERT INTO Comments (WorkItemId, AuthorId, Body, CreatedAt, EditedAt, IsDeleted, DeletedAt)
            VALUES (@web1, '{_bilal}', N'Looks good' + CHAR(10) + N'https://example.com/review', '{At:O}', NULL, 0, NULL),
                   (@web1, '{_amina}', N'Withdrawn', '{At:O}', NULL, 1, '{At:O}'),
                   (@web2, '{_owen}', N'Venue booked?', '{At:O}', NULL, 0, NULL);
            """);

        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.Database.MigrateAsync(Ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        var name = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        await using var drop = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task P4_US1_AS3_Descriptions_and_comments_read_as_before()
    {
        await using var db = AppDbContext.Create(_connectionString);

        var items = await db.WorkItems.AsNoTracking().OrderBy(w => w.Number).ToListAsync(Ct);
        Assert.All(items, w => Assert.Equal((TextFormat.Plain, w.Description), (w.DescriptionFormat, w.DescriptionText)));
        Assert.Equal("Line one\nSee https://example.com/plan", items[0].Description);
        Assert.Null(items[1].DescriptionText);

        var comments = await db.Comments.AsNoTracking().OrderBy(c => c.Id).ToListAsync(Ct);
        Assert.All(comments, c => Assert.Equal((TextFormat.Plain, c.Body), (c.BodyFormat, c.BodyText)));
        Assert.Equal("Looks good\nhttps://example.com/review", comments[0].Body);
    }

    [Fact]
    public async Task P4_US1_AS3_Each_items_creator_assignee_and_commenters_watch_it()
    {
        await using var db = AppDbContext.Create(_connectionString);

        var watchers = await db.Watches.AsNoTracking()
            .Join(db.WorkItems, w => w.WorkItemId, i => i.Id, (w, i) => new { i.Key, w.UserId })
            .ToListAsync(Ct);

        Assert.Equal(new[] { _owen, _amina, _bilal }.Order(), watchers.Where(w => w.Key == "WEB-1").Select(w => w.UserId).Order());
        Assert.Equal(new[] { _amina, _owen }.Order(), watchers.Where(w => w.Key == "WEB-2").Select(w => w.UserId).Order());
    }

    [Fact]
    public async Task P4_US5_Every_account_gets_the_emails_aimed_at_it()
    {
        await using var db = AppDbContext.Create(_connectionString);

        Assert.All(await db.Users.AsNoTracking().Select(u => u.EmailNotifications).ToListAsync(Ct), choice => Assert.Equal(EmailChoice.AimedAtMe, choice));
    }

    [Fact]
    public async Task P4_US3_Existing_items_are_found_once_the_search_index_is_filled()
    {
        _harness.CurrentUser.UserId = _bilal;
        Assert.Empty(await FindAsync("venue"));

        while (await _harness.CallAsync<ISearchIndexBackfill, int>(s => s.IndexPendingAsync(Ct)) > 0)
        {
        }

        Assert.Equal(["WEB-2"], await FindAsync("venue"));
        Assert.Equal(["WEB-1"], await FindAsync("review")); // in a comment
        Assert.Equal(["WEB-1"], await FindAsync("example.com plan")); // in the description
        Assert.Empty(await FindAsync("withdrawn")); // a deleted comment
    }

    private async Task<IReadOnlyList<string>> FindAsync(string words) =>
        (await _harness.CallAsync<ISearchService, Result<SearchResults>>(s => s.SearchAsync(new SearchQuery(words), PageRequest.First, Ct)))
            .ValueOrThrow().Rows.Items.Select(r => r.Key).ToList();

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
