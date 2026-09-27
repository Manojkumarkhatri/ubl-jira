using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <summary>Project teams (Phase 2 research R1), and the upgrade of existing projects (research R3, FR-014).</summary>
    public partial class ProjectMembers : Migration
    {
        /// <summary>Makes each project's owner its Project Admin and everyone who worked on it (created a work item,
        /// deleted ones included; acted in a work item's history; or commented) a Member, then audits each new
        /// membership. Existing memberships are skipped, so running it again adds nothing.</summary>
        public const string UpgradeSql = """
            DECLARE @now datetimeoffset = TODATETIMEOFFSET(SYSUTCDATETIME(), 0);
            DECLARE @added TABLE (ProjectId bigint NOT NULL, UserId uniqueidentifier NOT NULL, Role varchar(12) NOT NULL);

            INSERT INTO [ProjectMembers] ([ProjectId], [UserId], [Role], [AddedAt], [AddedById])
            OUTPUT inserted.[ProjectId], inserted.[UserId], inserted.[Role] INTO @added
            SELECT p.[Id], p.[OwnerId], 'ProjectAdmin', @now, NULL
            FROM [Projects] p
            WHERE NOT EXISTS (SELECT 1 FROM [ProjectMembers] m WHERE m.[ProjectId] = p.[Id] AND m.[UserId] = p.[OwnerId]);

            INSERT INTO [ProjectMembers] ([ProjectId], [UserId], [Role], [AddedAt], [AddedById])
            OUTPUT inserted.[ProjectId], inserted.[UserId], inserted.[Role] INTO @added
            SELECT c.[ProjectId], c.[UserId], 'Member', @now, NULL
            FROM (
                SELECT w.[ProjectId], w.[CreatedById] AS [UserId] FROM [WorkItems] w
                UNION
                SELECT w.[ProjectId], h.[ActorId] FROM [WorkItemChanges] h JOIN [WorkItems] w ON w.[Id] = h.[WorkItemId]
                UNION
                SELECT w.[ProjectId], c.[AuthorId] FROM [Comments] c JOIN [WorkItems] w ON w.[Id] = c.[WorkItemId]
            ) c
            WHERE NOT EXISTS (SELECT 1 FROM [ProjectMembers] m WHERE m.[ProjectId] = c.[ProjectId] AND m.[UserId] = c.[UserId]);

            INSERT INTO [AuditEvents] ([OccurredAt], [EventType], [ActorUserId], [SubjectUserId], [Target], [Details], [SourceIp])
            SELECT @now, 'MemberAdded', NULL, a.[UserId], p.[Key],
                CONCAT('{"Role":"', a.[Role], '","Source":"Phase 2 upgrade"}'), NULL
            FROM @added a JOIN [Projects] p ON p.[Id] = a.[ProjectId];
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MembersVersion",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_AspNetUsers_AddedById",
                        column: x => x.AddedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_AddedById",
                table: "ProjectMembers",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_UserId",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId")
                .Annotation("SqlServer:Include", new[] { "Role" });

            migrationBuilder.Sql(UpgradeSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "MembersVersion",
                table: "Projects");
        }
    }
}
