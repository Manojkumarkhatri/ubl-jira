using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.AddColumn<long>(
                name: "SprintId",
                table: "WorkItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    State = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.CheckConstraint("CK_Sprints_Dates", "[StartDate] IS NULL OR [EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_Sprints_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sprints_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SprintScopeEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SprintId = table.Column<long>(type: "bigint", nullable: false),
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    Estimate = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintScopeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintScopeEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SprintScopeEvents_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SprintScopeEvents_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Backlog",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL AND [Type] IN ('Task', 'Story', 'Bug')")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Type", "StatusId", "AssigneeId", "Estimate", "ContainerId", "SprintId", "ResolvedAt", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL AND [Type] IN ('Task', 'Story', 'Bug')")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion", "AssigneeId", "DueDate", "Type", "Estimate", "SprintId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Sprint",
                table: "WorkItems",
                columns: new[] { "SprintId", "Rank" },
                filter: "[IsDeleted] = 0 AND [SprintId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Type", "StatusId", "AssigneeId", "Estimate", "ContainerId", "ResolvedAt", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_CreatedById",
                table: "Sprints",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_OneActive",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[State] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_Number",
                table: "Sprints",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SprintScopeEvents_ActorId",
                table: "SprintScopeEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintScopeEvents_SprintId_Kind",
                table: "SprintScopeEvents",
                columns: new[] { "SprintId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_SprintScopeEvents_WorkItemId",
                table: "SprintScopeEvents",
                column: "WorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Sprints_SprintId",
                table: "WorkItems",
                column: "SprintId",
                principalTable: "Sprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The sprint report is read from this log, so its rows never change (Phase 3 research R11).
            migrationBuilder.Sql(AppendOnlyTable.CreateTrigger("SprintScopeEvents", "TR_SprintScopeEvents_AppendOnly"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlyTable.DropTrigger("TR_SprintScopeEvents_AppendOnly"));

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Sprints_SprintId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "SprintScopeEvents");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Backlog",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Sprint",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SprintId",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL AND [Type] IN ('Task', 'Story', 'Bug')")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion", "AssigneeId", "DueDate", "Type", "Estimate" });
        }
    }
}
