using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TemplatesAndTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Assignee_Live",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Project_Live",
                table: "WorkItems");

            migrationBuilder.AddColumn<long>(
                name: "ContainerId",
                table: "WorkItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Estimate",
                table: "WorkItems",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            // Every project created before Phase 3 becomes a Kanban project (Phase 3 FR-004, research R24).
            migrationBuilder.AddColumn<string>(
                name: "Template",
                table: "Projects",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "Kanban");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Assignee_Live",
                table: "WorkItems",
                columns: new[] { "AssigneeId", "ProjectId" },
                filter: "[IsDeleted] = 0 AND [AssigneeId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "ParentId", "StatusId", "Priority", "DueDate", "ResolvedAt", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL AND [Type] IN ('Task', 'Story', 'Bug')")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion", "AssigneeId", "DueDate", "Type", "Estimate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Container_Live",
                table: "WorkItems",
                column: "ContainerId",
                filter: "[IsDeleted] = 0 AND [ContainerId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Type", "StatusId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Project_Live",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Number" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "ParentId", "StatusId", "Priority", "AssigneeId", "StartDate", "DueDate", "UpdatedAt", "Rank", "RowVersion", "Type", "ContainerId", "Estimate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkItems_Estimate",
                table: "WorkItems",
                sql: "[Estimate] IS NULL OR [Estimate] BETWEEN 0 AND 999.9");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkItems_Milestone",
                table: "WorkItems",
                sql: "[Type] <> 'Milestone' OR ([StartDate] IS NULL AND [Estimate] IS NULL AND [ParentId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItems_ContainerId",
                table: "WorkItems",
                column: "ContainerId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItems_ContainerId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Assignee_Live",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Container_Live",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Project_Live",
                table: "WorkItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkItems_Estimate",
                table: "WorkItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkItems_Milestone",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ContainerId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Estimate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Template",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Assignee_Live",
                table: "WorkItems",
                columns: new[] { "AssigneeId", "ProjectId" },
                filter: "[IsDeleted] = 0 AND [AssigneeId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "ParentId", "StatusId", "Priority", "DueDate", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion", "AssigneeId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Project_Live",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Number" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "ParentId", "StatusId", "Priority", "AssigneeId", "StartDate", "DueDate", "UpdatedAt", "Rank", "RowVersion" });
        }
    }
}
