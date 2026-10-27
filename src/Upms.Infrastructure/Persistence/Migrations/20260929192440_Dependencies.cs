using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Dependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dependencies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    PredecessorId = table.Column<long>(type: "bigint", nullable: false),
                    SuccessorId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependencies", x => x.Id);
                    table.CheckConstraint("CK_Dependencies_NotSelf", "[PredecessorId] <> [SuccessorId]");
                    table.ForeignKey(
                        name: "FK_Dependencies_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dependencies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dependencies_WorkItems_PredecessorId",
                        column: x => x.PredecessorId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dependencies_WorkItems_SuccessorId",
                        column: x => x.SuccessorId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_CreatedById",
                table: "Dependencies",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_PredecessorId_SuccessorId",
                table: "Dependencies",
                columns: new[] { "PredecessorId", "SuccessorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_Project",
                table: "Dependencies",
                column: "ProjectId")
                .Annotation("SqlServer:Include", new[] { "PredecessorId", "SuccessorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_SuccessorId",
                table: "Dependencies",
                column: "SuccessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dependencies");
        }
    }
}
