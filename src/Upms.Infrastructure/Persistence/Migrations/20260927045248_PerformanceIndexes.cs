using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Status_Live",
                table: "WorkItems",
                column: "StatusId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems",
                columns: new[] { "ParentId", "StatusId" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Status_Live",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Subtasks_Live",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL");
        }
    }
}
