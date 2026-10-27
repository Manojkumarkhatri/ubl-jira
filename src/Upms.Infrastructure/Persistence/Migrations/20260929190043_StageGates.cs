using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StageGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageGates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    StageId = table.Column<long>(type: "bigint", nullable: false),
                    RequiredApprovals = table.Column<int>(type: "int", nullable: true),
                    ExcludeRequester = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    State = table.Column<string>(type: "varchar(18)", unicode: false, maxLength: 18, nullable: false),
                    CurrentRequestId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageGates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageGates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageGates_WorkItems_StageId",
                        column: x => x.StageId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GateApprovers",
                columns: table => new
                {
                    GateId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateApprovers", x => new { x.GateId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GateApprovers_AspNetUsers_AddedById",
                        column: x => x.AddedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateApprovers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateApprovers_StageGates_GateId",
                        column: x => x.GateId,
                        principalTable: "StageGates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GateRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GateId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateRequests_AspNetUsers_ClosedById",
                        column: x => x.ClosedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateRequests_AspNetUsers_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateRequests_StageGates_GateId",
                        column: x => x.GateId,
                        principalTable: "StageGates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GateDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GateDecisions_AspNetUsers_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GateDecisions_GateRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "GateRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateApprovers_AddedById",
                table: "GateApprovers",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_GateApprovers_UserId",
                table: "GateApprovers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GateDecisions_ApproverId",
                table: "GateDecisions",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_GateDecisions_RequestId_ApproverId",
                table: "GateDecisions",
                columns: new[] { "RequestId", "ApproverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateRequests_ClosedById",
                table: "GateRequests",
                column: "ClosedById");

            migrationBuilder.CreateIndex(
                name: "IX_GateRequests_GateId_Number",
                table: "GateRequests",
                columns: new[] { "GateId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GateRequests_Pending",
                table: "GateRequests",
                column: "GateId",
                filter: "[Outcome] = 'Pending'")
                .Annotation("SqlServer:Include", new[] { "Number", "RequestedById", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GateRequests_RequestedById",
                table: "GateRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_StageGates_ProjectId",
                table: "StageGates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StageGates_StageId",
                table: "StageGates",
                column: "StageId",
                unique: true);

            // The governance trail: decisions are never changed or removed (Phase 3 research R14).
            migrationBuilder.Sql(AppendOnlyTable.CreateTrigger("GateDecisions", "TR_GateDecisions_AppendOnly"));

            // Every stage ends in a gate (FR-031): stages made before this migration, deleted ones included so that a
            // restored stage has its gate, get one that is not requested and needs all of its approvers.
            migrationBuilder.Sql("""
                INSERT INTO [StageGates] ([ProjectId], [StageId], [RequiredApprovals], [ExcludeRequester], [State])
                SELECT w.[ProjectId], w.[Id], NULL, 1, 'NotRequested'
                FROM [WorkItems] w
                WHERE w.[Type] = 'Stage' AND NOT EXISTS (SELECT 1 FROM [StageGates] g WHERE g.[StageId] = w.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AppendOnlyTable.DropTrigger("TR_GateDecisions_AppendOnly"));

            migrationBuilder.DropTable(
                name: "GateApprovers");

            migrationBuilder.DropTable(
                name: "GateDecisions");

            migrationBuilder.DropTable(
                name: "GateRequests");

            migrationBuilder.DropTable(
                name: "StageGates");
        }
    }
}
