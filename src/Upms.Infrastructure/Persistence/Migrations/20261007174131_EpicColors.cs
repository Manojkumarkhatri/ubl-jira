using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EpicColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems");

            migrationBuilder.AddColumn<byte>(
                name: "Color",
                table: "WorkItems",
                type: "tinyint",
                nullable: true);

            // Existing epics take the color a new one would have (Jira parity pass FR-025; research R15, R21). WorkItems is an
            // ordinary table; history is not touched.
            migrationBuilder.Sql("UPDATE WorkItems SET Color = ([Number] - 1) % 8 WHERE [Type] = 'Epic';");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems",
                columns: new[] { "ParentId", "StatusId" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "ProjectId", "Key", "Title", "Priority", "Rank", "AssigneeId", "DueDate", "ResolvedAt", "RowVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems",
                columns: new[] { "ParentId", "StatusId" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NOT NULL");
        }
    }
}
