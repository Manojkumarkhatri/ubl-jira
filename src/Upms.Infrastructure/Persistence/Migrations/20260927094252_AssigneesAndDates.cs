using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssigneesAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeId",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "WorkItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "WorkItems",
                type: "date",
                nullable: true);

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkItems_Dates",
                table: "WorkItems",
                sql: "([StartDate] IS NULL OR [StartDate] BETWEEN '2000-01-01' AND '2099-12-31') AND ([DueDate] IS NULL OR [DueDate] BETWEEN '2000-01-01' AND '2099-12-31') AND ([StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate])");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_AspNetUsers_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_AspNetUsers_AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Assignee_Live",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Project_Live",
                table: "WorkItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkItems_Dates",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "Priority", "ResolvedAt", "RowVersion" });
        }
    }
}
