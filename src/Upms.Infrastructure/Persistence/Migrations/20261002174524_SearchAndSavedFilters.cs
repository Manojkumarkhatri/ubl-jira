using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SearchAndSavedFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SearchIndexedAt",
                table: "WorkItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecentViews",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentViews", x => new { x.UserId, x.WorkItemId });
                    table.ForeignKey(
                        name: "FK_RecentViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecentViews_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedFilters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedFilters_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SearchTerms",
                columns: table => new
                {
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchTerms", x => new { x.WorkItemId, x.Term });
                    table.ForeignKey(
                        name: "FK_SearchTerms_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedFilterShares",
                columns: table => new
                {
                    SavedFilterId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    SharedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilterShares", x => new { x.SavedFilterId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_SavedFilterShares_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedFilterShares_SavedFilters_SavedFilterId",
                        column: x => x.SavedFilterId,
                        principalTable: "SavedFilters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_NotIndexed",
                table: "WorkItems",
                column: "Id",
                filter: "[SearchIndexedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecentViews_Latest",
                table: "RecentViews",
                columns: new[] { "UserId", "ViewedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RecentViews_WorkItemId",
                table: "RecentViews",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_OwnerId_NormalizedName",
                table: "SavedFilters",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilterShares_ProjectId",
                table: "SavedFilterShares",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchTerms_Term",
                table: "SearchTerms",
                column: "Term")
                .Annotation("SqlServer:Include", new[] { "ProjectId", "WorkItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecentViews");

            migrationBuilder.DropTable(
                name: "SavedFilterShares");

            migrationBuilder.DropTable(
                name: "SearchTerms");

            migrationBuilder.DropTable(
                name: "SavedFilters");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_NotIndexed",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SearchIndexedAt",
                table: "WorkItems");
        }
    }
}
