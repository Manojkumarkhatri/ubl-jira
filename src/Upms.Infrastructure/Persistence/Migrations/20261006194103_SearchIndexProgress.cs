using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchIndexProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_NotIndexed",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SearchIndexedAt",
                table: "WorkItems");

            migrationBuilder.CreateTable(
                name: "SearchIndexProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastWorkItemId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexProgress", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchIndexProgress");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SearchIndexedAt",
                table: "WorkItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_NotIndexed",
                table: "WorkItems",
                column: "Id",
                filter: "[SearchIndexedAt] IS NULL");
        }
    }
}
