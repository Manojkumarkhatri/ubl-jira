using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpaceNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SidebarCollapsed",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProjectVisits",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    VisitedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVisits", x => new { x.UserId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectVisits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectVisits_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StarredProjects",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    StarredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarredProjects", x => new { x.UserId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_StarredProjects_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StarredProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Summary",
                table: "WorkItems",
                column: "ProjectId",
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Type", "Priority", "StatusId", "AssigneeId", "ContainerId", "ParentId", "CreatedAt", "UpdatedAt", "ResolvedAt", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemChanges_OccurredAt",
                table: "WorkItemChanges",
                column: "OccurredAt")
                .Annotation("SqlServer:Include", new[] { "WorkItemId", "ActorId", "Field" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVisits_ProjectId",
                table: "ProjectVisits",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVisits_UserId_VisitedAt",
                table: "ProjectVisits",
                columns: new[] { "UserId", "VisitedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_StarredProjects_ProjectId",
                table: "StarredProjects",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectVisits");

            migrationBuilder.DropTable(
                name: "StarredProjects");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Summary",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemChanges_OccurredAt",
                table: "WorkItemChanges");

            migrationBuilder.DropColumn(
                name: "SidebarCollapsed",
                table: "AspNetUsers");
        }
    }
}
