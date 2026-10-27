using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Infrastructure.Persistence;

namespace Upms.Application.Tests.Work;

/// <summary>The Jira parity pass upgrade changes nothing people made (Jira parity pass SC-005; research R20, R21): a database
/// at the last Phase 4 migration, loaded with Phase 4 data, comes out of the three new migrations with every space offering
/// all four work types, every epic in its color, every history row as it was, and sub-tasks shown as cards.</summary>
public sealed class JiraParityUpgradeTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string LastPhase4Migration = "20261006194103_SearchIndexProgress";
    private static readonly DateTimeOffset At = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = $"UpmsUpgradeJp_{Guid.NewGuid():N}",
    }.ConnectionString;

    private ServiceHarness _harness = null!;
    private readonly Guid _owen = Guid.NewGuid();
    private List<string> _historyBefore = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(LastPhase4Migration, Ct);
        }

        _harness = new ServiceHarness(_connectionString);

        // Phase 4 data, written with the Phase 4 schema: Owen's Kanban space WEB with epics WEB-1 and WEB-10, story WEB-2 in
        // WEB-1 with sub-task WEB-3, and their history.
        await ExecuteAsync($"""
            INSERT INTO AspNetUsers (Id, DisplayName, OrganizationRole, IsActive, MustChangePassword, CreatedAt, UserName, NormalizedUserName,
                Email, NormalizedEmail, EmailConfirmed, SecurityStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
                EmailNotifications)
            VALUES ('{_owen}', N'Owen Tester', 'User', 1, 0, '{At:O}', N'owen', N'OWEN', N'owen@example.com', N'OWEN@EXAMPLE.COM', 1,
                N'{Guid.NewGuid():N}', 0, 0, 1, 0, 'AimedAtMe');
            INSERT INTO Projects ([Key], Name, NormalizedName, Description, OwnerId, NextItemNumber, BoardVersion, DetailsVersion, MembersVersion, CreatedAt, UpdatedAt, Template)
            VALUES ('WEB', N'Website Revamp', N'WEBSITE REVAMP', NULL, '{_owen}', 11, 1, 1, 1, '{At:O}', '{At:O}', 'Kanban');
            DECLARE @web bigint = (SELECT Id FROM Projects WHERE [Key] = 'WEB');
            INSERT INTO ProjectStatuses (ProjectId, Name, NormalizedName, Category, Position, WipLimit)
            VALUES (@web, N'To Do', N'TO DO', 'ToDo', 0, NULL), (@web, N'In Progress', N'IN PROGRESS', 'InProgress', 1, NULL),
                   (@web, N'Done', N'DONE', 'Done', 2, NULL);
            INSERT INTO ProjectMembers (ProjectId, UserId, Role, AddedAt, AddedById) VALUES (@web, '{_owen}', 'ProjectAdmin', '{At:O}', NULL);
            DECLARE @todo bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web AND Name = N'To Do');
            DECLARE @doing bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web AND Name = N'In Progress');
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, DescriptionFormat, DescriptionText, Priority,
                StatusId, [Rank], CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, AssigneeId)
            VALUES (@web, 1, 'WEB-1', 'Epic', NULL, N'Card refunds', NULL, 0, NULL, 'Medium', @todo, 'a0', '{_owen}', '{At:O}', '{At:O}', NULL, 0, NULL),
                   (@web, 10, 'WEB-10', 'Epic', NULL, N'Wallet', NULL, 0, NULL, 'Medium', @todo, 'a1', '{_owen}', '{At:O}', '{At:O}', NULL, 0, NULL),
                   (@web, 2, 'WEB-2', 'Story', NULL, N'Refund to card', NULL, 0, NULL, 'High', @todo, 'a2', '{_owen}', '{At:O}', '{At:O}', NULL, 0, '{_owen}');
            DECLARE @epic bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-1');
            DECLARE @story bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-2');
            UPDATE WorkItems SET ContainerId = @epic WHERE Id = @story;
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, DescriptionFormat, DescriptionText, Priority,
                StatusId, [Rank], CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, AssigneeId)
            VALUES (@web, 3, 'WEB-3', 'Subtask', @story, N'Write the API', NULL, 0, NULL, 'Medium', @doing, 'a0', '{_owen}', '{At:O}', '{At:O}', NULL, 0, NULL);
            INSERT INTO WorkItemChanges (WorkItemId, ChangeSetId, ActorId, OccurredAt, Field, OldValue, NewValue, Note)
            SELECT Id, NEWID(), '{_owen}', '{At:O}', 'Created', NULL, N'To Do', NULL FROM WorkItems;
            INSERT INTO WorkItemChanges (WorkItemId, ChangeSetId, ActorId, OccurredAt, Field, OldValue, NewValue, Note)
            VALUES (@story, NEWID(), '{_owen}', '{At.AddMinutes(5):O}', 'Container', NULL, N'WEB-1 Card refunds', NULL);
            """);
        _historyBefore = await HistoryAsync();

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
    public async Task Every_space_offers_all_four_work_types()
    {
        await using var db = AppDbContext.Create(_connectionString);

        Assert.All(await db.Projects.AsNoTracking().Select(p => p.WorkTypes).ToListAsync(Ct), types => Assert.Equal(WorkTypeSet.All, types));
    }

    [Fact]
    public async Task Every_epic_has_the_color_a_new_one_would_have()
    {
        await using var db = AppDbContext.Create(_connectionString);

        var colors = await db.WorkItems.AsNoTracking().OrderBy(w => w.Number).Select(w => new { w.Key, w.Color }).ToListAsync(Ct);

        Assert.Equal([("WEB-1", (EpicColor?)EpicColor.Purple), ("WEB-2", null), ("WEB-3", null), ("WEB-10", EpicColor.Blue)],
            colors.Select(c => (c.Key, c.Color)));
    }

    [Fact]
    public async Task Every_history_row_is_unchanged()
    {
        Assert.Equal(_historyBefore, await HistoryAsync());
    }

    [Fact]
    public async Task Sub_tasks_show_on_the_board_with_their_parents_epic()
    {
        _harness.CurrentUser.UserId = _owen;

        var board = (await _harness.CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();

        var subtask = board.Columns.SelectMany(c => c.Cards).Single(c => c.Key == "WEB-3");
        Assert.Equal(("In Progress", "WEB-2", "WEB-1", EpicColor.Purple),
            (board.Columns.Single(c => c.Id == subtask.ColumnId).Name, subtask.ParentKey, subtask.Container?.Key, subtask.Container?.Color));
    }

    private async Task<List<string>> HistoryAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(
            "SELECT CONCAT(Id, '|', WorkItemId, '|', ChangeSetId, '|', ActorId, '|', OccurredAt, '|', Field, '|', OldValue, '|', NewValue, '|', Note) " +
            "FROM WorkItemChanges ORDER BY Id", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
