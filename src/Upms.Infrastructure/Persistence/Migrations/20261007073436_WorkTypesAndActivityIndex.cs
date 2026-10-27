using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkTypesAndActivityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemChanges_ActorId",
                table: "WorkItemChanges");

            migrationBuilder.AddColumn<int>(
                name: "WorkTypes",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemChanges_Actor",
                table: "WorkItemChanges",
                columns: new[] { "ActorId", "OccurredAt" })
                .Annotation("SqlServer:Include", new[] { "WorkItemId", "Field" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemChanges_Actor",
                table: "WorkItemChanges");

            migrationBuilder.DropColumn(
                name: "WorkTypes",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemChanges_ActorId",
                table: "WorkItemChanges",
                column: "ActorId");
        }
    }
}
