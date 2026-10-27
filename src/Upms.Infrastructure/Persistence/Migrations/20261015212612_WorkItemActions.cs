using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CoverAttachmentId",
                table: "WorkItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverColor",
                table: "WorkItems",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalEstimateMinutes",
                table: "WorkItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingEstimateMinutes",
                table: "WorkItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeSpentMinutes",
                table: "WorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkItemVotes",
                columns: table => new
                {
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemVotes", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkItemVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemVotes_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLogs", x => x.Id);
                    table.CheckConstraint("CK_WorkLogs_Minutes", "[Minutes] BETWEEN 1 AND 2397600");
                    table.ForeignKey(
                        name: "FK_WorkLogs_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkLogs_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemVotes_UserId",
                table: "WorkItemVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_AuthorId",
                table: "WorkLogs",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_WorkItemId_StartedAt",
                table: "WorkLogs",
                columns: new[] { "WorkItemId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemVotes");

            migrationBuilder.DropTable(
                name: "WorkLogs");

            migrationBuilder.DropColumn(
                name: "CoverAttachmentId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CoverColor",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "OriginalEstimateMinutes",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RemainingEstimateMinutes",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TimeSpentMinutes",
                table: "WorkItems");
        }
    }
}
