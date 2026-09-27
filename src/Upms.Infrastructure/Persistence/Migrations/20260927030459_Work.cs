using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Work : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "varchar(21)", unicode: false, maxLength: 21, nullable: false),
                    Type = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    StatusId = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false, collation: "Latin1_General_BIN2"),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_AspNetUsers_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_ProjectStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ProjectStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItems_WorkItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    ChangeSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Field = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemChanges_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemChanges_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemChanges_ActorId",
                table: "WorkItemChanges",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemChanges_WorkItemId_OccurredAt",
                table: "WorkItemChanges",
                columns: new[] { "WorkItemId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Board",
                table: "WorkItems",
                columns: new[] { "ProjectId", "StatusId", "Rank" },
                filter: "[IsDeleted] = 0 AND [ParentId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_CreatedById",
                table: "WorkItems",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_DeletedById",
                table: "WorkItems",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Key",
                table: "WorkItems",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_Number",
                table: "WorkItems",
                columns: new[] { "ProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId_ResolvedAt",
                table: "WorkItems",
                columns: new[] { "ProjectId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_StatusId",
                table: "WorkItems",
                column: "StatusId");

            migrationBuilder.Sql(AppendOnlyTable.CreateTrigger("WorkItemChanges", "TR_WorkItemChanges_AppendOnly"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlyTable.DropTrigger("TR_WorkItemChanges_AppendOnly"));

            migrationBuilder.DropTable(
                name: "WorkItemChanges");

            migrationBuilder.DropTable(
                name: "WorkItems");
        }
    }
}
