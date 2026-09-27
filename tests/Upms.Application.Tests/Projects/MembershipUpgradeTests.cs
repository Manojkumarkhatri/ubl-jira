using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Infrastructure.Persistence;
using Upms.Infrastructure.Persistence.Migrations;

namespace Upms.Application.Tests.Projects;

/// <summary>The Phase 2 upgrade of pilot projects (Phase 2 FR-014, SC-006, research R3), on a database of its own that is
/// first migrated to the last Phase 1 migration and loaded with pilot-style data.</summary>
public sealed class MembershipUpgradeTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string LastPhase1Migration = "20260927045248_PerformanceIndexes";
    private static readonly DateTimeOffset At = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = $"UpmsUpgrade_{Guid.NewGuid():N}",
    }.ConnectionString;

    private ServiceHarness _harness = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using (var db = AppDbContext.Create(_connectionString))
        {
            await db.GetService<IMigrator>().MigrateAsync(LastPhase1Migration, Ct);
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
    public async Task P2_US1_AS9_Owners_become_Project_Admins_and_everyone_who_worked_on_a_project_becomes_a_Member()
    {
        var data = new TestData(_harness);
        var owen = await data.UserAsync("owen");
        var cara = await data.UserAsync("cara");
        var erin = await data.UserAsync("erin");
        var dan = await data.UserAsync("dan");
        var gone = await data.UserAsync("gone", isActive: false);
        var uma = await data.UserAsync("uma");
        var olga = await data.UserAsync("olga", isActive: false);

        // Phase 1 data, written with the Phase 1 schema: WEB owned by owen, where cara created WEB-1 and a later deleted
        // WEB-2, erin changed WEB-1, dan commented on it and gone (now deactivated) created WEB-3; OPS, owned by olga
        // (now deactivated), has no work. uma never worked anywhere.
        await ExecuteAsync($"""
            INSERT INTO Projects ([Key], Name, NormalizedName, Description, OwnerId, NextItemNumber, BoardVersion, DetailsVersion, CreatedAt, UpdatedAt)
            VALUES ('WEB', N'Website Revamp', N'WEBSITE REVAMP', NULL, '{owen.Id}', 4, 1, 1, '{At:O}', '{At:O}'),
                   ('OPS', N'Operations', N'OPERATIONS', NULL, '{olga.Id}', 1, 1, 1, '{At:O}', '{At:O}');
            INSERT INTO ProjectStatuses (ProjectId, Name, NormalizedName, Category, Position, WipLimit)
            SELECT Id, N'To Do', N'TO DO', 'ToDo', 0, NULL FROM Projects;
            DECLARE @web bigint = (SELECT Id FROM Projects WHERE [Key] = 'WEB');
            DECLARE @todo bigint = (SELECT Id FROM ProjectStatuses WHERE ProjectId = @web);
            INSERT INTO WorkItems (ProjectId, Number, [Key], [Type], ParentId, Title, Description, Priority, StatusId, [Rank],
                CreatedById, CreatedAt, UpdatedAt, ResolvedAt, IsDeleted, DeletedAt, DeletedById)
            VALUES (@web, 1, 'WEB-1', 'Task', NULL, N'Design the home page', NULL, 'Medium', @todo, 'a', '{cara.Id}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL),
                   (@web, 2, 'WEB-2', 'Task', NULL, N'Old idea', NULL, 'Medium', @todo, 'b', '{cara.Id}', '{At:O}', '{At:O}', NULL, 1, '{At:O}', '{owen.Id}'),
                   (@web, 3, 'WEB-3', 'Task', NULL, N'Write the copy', NULL, 'Medium', @todo, 'c', '{gone.Id}', '{At:O}', '{At:O}', NULL, 0, NULL, NULL);
            DECLARE @web1 bigint = (SELECT Id FROM WorkItems WHERE [Key] = 'WEB-1');
            INSERT INTO WorkItemChanges (WorkItemId, ChangeSetId, ActorId, OccurredAt, Field, OldValue, NewValue, Note)
            VALUES (@web1, NEWID(), '{erin.Id}', '{At:O}', 'Priority', 'Medium', 'High', NULL);
            INSERT INTO Comments (WorkItemId, AuthorId, Body, CreatedAt, EditedAt, IsDeleted, DeletedAt)
            VALUES (@web1, '{dan.Id}', N'Looks good', '{At:O}', NULL, 0, NULL);
            """);

        await MigrateToLatestAsync();

        var members = await MembersAsync();
        Assert.Equal(
            new[]
            {
                ("OPS", olga.Id, ProjectRole.ProjectAdmin),
                ("WEB", owen.Id, ProjectRole.ProjectAdmin),
                ("WEB", cara.Id, ProjectRole.Member),
                ("WEB", dan.Id, ProjectRole.Member),
                ("WEB", erin.Id, ProjectRole.Member),
                ("WEB", gone.Id, ProjectRole.Member),
            }.OrderBy(m => m.Item1).ThenBy(m => m.Item2).ToList(),
            members.Select(m => (m.Key, m.UserId, m.Role)).OrderBy(m => m.Key).ThenBy(m => m.UserId).ToList());
        Assert.DoesNotContain(members, m => m.UserId == uma.Id);
        Assert.All(members, m => Assert.Null(m.AddedById));

        var audit = await UpgradeAuditAsync();
        Assert.Equal(members.Count, audit.Count);
        Assert.All(audit, e =>
        {
            Assert.Null(e.ActorUserId);
            Assert.Contains("\"Source\":\"Phase 2 upgrade\"", e.Details, StringComparison.Ordinal);
        });
        Assert.Contains(audit, e => e.SubjectUserId == owen.Id && e.Target == "WEB" && e.Details!.Contains("\"Role\":\"ProjectAdmin\"", StringComparison.Ordinal));

        // Running the upgrade again adds nothing.
        await ExecuteAsync(ProjectMembers.UpgradeSql);
        Assert.Equal(members.Count, (await MembersAsync()).Count);
        Assert.Equal(audit.Count, (await UpgradeAuditAsync()).Count);
    }

    private async Task MigrateToLatestAsync()
    {
        await using var db = AppDbContext.Create(_connectionString);
        await db.Database.MigrateAsync(Ct);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<List<(string Key, Guid UserId, ProjectRole Role, Guid? AddedById)>> MembersAsync()
    {
        await using var db = AppDbContext.Create(_connectionString);
        var rows = await db.ProjectMembers.AsNoTracking()
            .Join(db.Projects, m => m.ProjectId, p => p.Id, (m, p) => new { p.Key, m.UserId, m.Role, m.AddedById })
            .ToListAsync(Ct);
        return rows.ConvertAll(r => (r.Key, r.UserId, r.Role, r.AddedById));
    }

    private async Task<List<AuditEvent>> UpgradeAuditAsync()
    {
        await using var db = AppDbContext.Create(_connectionString);
        return await db.AuditEvents.AsNoTracking().Where(e => e.EventType == AuditEventType.MemberAdded).ToListAsync(Ct);
    }
}
