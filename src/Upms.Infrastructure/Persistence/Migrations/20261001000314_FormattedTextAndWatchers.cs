using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormattedTextAndWatchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DescriptionFormat",
                table: "WorkItems",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionText",
                table: "WorkItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "BodyFormat",
                table: "Comments",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "BodyText",
                table: "Comments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "WorkItemWatchers",
                columns: table => new
                {
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemWatchers", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkItemWatchers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemWatchers_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemWatchers_UserId",
                table: "WorkItemWatchers",
                column: "UserId");

            // Existing text stays plain and reads as before; its plain-text copy is the text itself (research R4, SC-008).
            migrationBuilder.Sql("UPDATE [WorkItems] SET [DescriptionText] = [Description] WHERE [Description] IS NOT NULL;");
            migrationBuilder.Sql("UPDATE [Comments] SET [BodyText] = [Body];");

            // Existing work behaves as the spec describes: each item's creator, assignee and commenters watch it (R7).
            migrationBuilder.Sql("""
                INSERT INTO [WorkItemWatchers] ([WorkItemId], [UserId], [StartedAt])
                SELECT [WorkItemId], [UserId], SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00')
                FROM (
                    SELECT [Id] AS [WorkItemId], [CreatedById] AS [UserId] FROM [WorkItems]
                    UNION SELECT [Id], [AssigneeId] FROM [WorkItems] WHERE [AssigneeId] IS NOT NULL
                    UNION SELECT [WorkItemId], [AuthorId] FROM [Comments]
                ) AS [People];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemWatchers");

            migrationBuilder.DropColumn(
                name: "DescriptionFormat",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DescriptionText",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "BodyFormat",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "BodyText",
                table: "Comments");
        }
    }
}
