using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Work;
using Upms.Infrastructure.Persistence;

namespace Upms.Application.Tests.Projects;

/// <summary>The Phase 3 upgrade changes nothing visible (Phase 3 FR-004, SC-010, research R24): a database migrated to
/// the last Phase 2 migration and loaded with pilot-style data comes out with every project Kanban and its work, columns,
/// team and history as they were.</summary>
public sealed class TemplatesUpgradeTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string LastPhase2Migration = "20260927094252_AssigneesAndDates";
    private static readonly DateTimeOffset At = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = $"UpmsUpgrade3_{Guid.NewGuid():N}",
    }.ConnectionString;

    private ServiceHarness _harness = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(LastPhase2Migration, Ct);
        }

        _harness = new ServiceHarness(_connectionString);
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
    public async Task P3_US1_AS3_Every_existing_project_becomes_Kanban_and_its_work_is_unchanged()
    {
        var owen = await UserAsync("owen");
        var amina = await UserAsync("amina");

        // Phase 2 data, written with the Phase 2 schema: WEB with customized columns (Backlog, Doing, Review, Shipped),
        // an assigned and scheduled task WEB-1 with a sub-task WEB-2, a deleted task WEB-3, a team and a history row.
        await ExecuteAsync($"""
            INSERT INTO Projects ([Key], Name, NormalizedName, Description, OwnerId, NextItemNumber, BoardVersion, DetailsVersion, MembersVersion, CreatedAt, UpdatedAt)
            VALUES ('WEB', N'Website Revamp', N'WEBSITE REVAMP', NULL, '{owen}', 4, 3, 1, 2, '{At:O}', '{At:O}');
            DECLARE @web bigint = (SELECT Id FROM Projects WHERE [Key] = 'WEB');
            INSERT INTO ProjectStatuses (ProjectId, Name, NormalizedName, Category, Position, WipLimit)
            VALUES (@web, N'Backlog', N'BACKLOG', 'ToDo', 0, NULL), (@web, N'Doing', N'DOING', 'InProgress', 1, 3),
                   (@web, N'Review', N'REVIEW', 'InProgress', 2, NULL), (@web, N'Shipped', N'SHIPPED', 'Done', 3, NULL);
            INSERT INTO ProjectMembers (ProjectId, UserId, Role, AddedAt, AddedById)
            VALUES (@web, '{owen}', 'ProjectAdmin', '{At:O}', NULL), (@web, '{amina}', 'Member', '{At:O}', '{owen}');
            DECLARE @doing bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web AND Name = N'Doing');
            DECLARE @backlog bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web AND Name = N'Backlog');
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, Priority, StatusId, [Rank],
                CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, DeletedAt, DeletedById, AssigneeId, StartDate, DueDate)
            VALUES (@web, 1, 'WEB-1', 'Task', NULL, N'Design the home page', N'Hero first', 'High', @doing, 'a0', '{owen}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL, '{amina}', '2026-10-01', '2026-10-10'),
                   (@web, 3, 'WEB-3', 'Task', NULL, N'Old idea', NULL, 'Medium', @backlog, 'a0', '{owen}', '{At:O}', '{At:O}', NULL, 1, '{At:O}', '{owen}', NULL, NULL, NULL);
            DECLARE @web1 bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-1');
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, Priority, StatusId, [Rank],
                CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, DeletedAt, DeletedById, AssigneeId, StartDate, DueDate)
            VALUES (@web, 2, 'WEB-2', 'Subtask', @web1, N'Pick the photo', NULL, 'Medium', @backlog, 'a0', '{amina}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL, NULL, NULL, '2026-10-05');
            INSERT INTO WorkItemChanges (WorkItemId, ChangeSetId, ActorId, OccurredAt, Field, OldValue, NewValue, Note)
            VALUES (@web1, NEWID(), '{amina}', '{At:O}', 'Priority', 'Medium', 'High', NULL);
            """);
        var before = await SnapshotAsync();

        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.Database.MigrateAsync(Ct);
        }

        await using var after = AppDbContext.Create(_connectionString);
        var project = await after.Projects.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(ProjectTemplate.Kanban, project.Template);

        var items = await after.WorkItems.IgnoreQueryFilters().AsNoTracking().OrderBy(w => w.Number).ToListAsync(Ct);
        Assert.Equal([WorkItemType.Task, WorkItemType.Subtask, WorkItemType.Task], items.Select(w => w.Type));
        Assert.All(items, w =>
        {
            Assert.Null(w.ContainerId);
            Assert.Null(w.Estimate);
        });

        Assert.Equal(before, await SnapshotAsync());
    }

    /// <summary>Everything Phase 2 stored about the project, as text, for a before-and-after comparison.</summary>
    private async Task<string> SnapshotAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand("""
            SELECT (SELECT [Key], Name, OwnerId, NextItemNumber, BoardVersion, DetailsVersion, MembersVersion FROM Projects FOR JSON PATH),
                   (SELECT Name, Category, Position, WipLimit FROM ProjectStatuses ORDER BY Position FOR JSON PATH),
                   (SELECT UserId, Role, AddedById FROM ProjectMembers ORDER BY UserId FOR JSON PATH),
                   (SELECT [Key], [Type], ParentId, Title, Description, Priority, StatusId, [Rank], AssigneeId, StartDate, DueDate,
                           ResolvedAt, IsDeleted FROM WorkItems ORDER BY Number FOR JSON PATH),
                   (SELECT WorkItemId, ActorId, Field, OldValue, NewValue FROM WorkItemChanges ORDER BY Id FOR JSON PATH)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        await reader.ReadAsync(Ct);
        return string.Join("\n", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : reader.GetString(i)));
    }

    /// <summary>A Phase 2 account, written with the Phase 2 schema (later phases add columns the account model now needs).</summary>
    private async Task<Guid> UserAsync(string name)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync($"""
            INSERT INTO AspNetUsers (Id, DisplayName, OrganizationRole, IsActive, MustChangePassword, CreatedAt, UserName, NormalizedUserName,
                Email, NormalizedEmail, EmailConfirmed, SecurityStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES ('{id}', N'{name} Tester', 'User', 1, 0, '{At:O}', N'{name}', N'{name.ToUpperInvariant()}',
                N'{name}@example.com', N'{name.ToUpperInvariant()}@EXAMPLE.COM', 1, N'{Guid.NewGuid():N}', 0, 0, 1, 0);
            """);
        return id;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
