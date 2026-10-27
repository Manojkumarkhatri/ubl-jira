using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Attachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CheckStatus = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CheckAttempts = table.Column<int>(type: "int", nullable: false),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ThreatName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PurgedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_Size", "[Size] BETWEEN 1 AND 26214400");
                    table.ForeignKey(
                        name: "FK_Attachments_AspNetUsers_PurgedById",
                        column: x => x.PurgedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_AspNetUsers_RemovedById",
                        column: x => x.RemovedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentContents",
                columns: table => new
                {
                    AttachmentId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentContents", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_AttachmentContents_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Checking",
                table: "Attachments",
                column: "NextCheckAt",
                filter: "[CheckStatus] = 'Checking'");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Item_Listed",
                table: "Attachments",
                column: "WorkItemId",
                filter: "[RemovedAt] IS NULL AND [PurgedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_PurgedById",
                table: "Attachments",
                column: "PurgedById");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_RemovedById",
                table: "Attachments",
                column: "RemovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedById",
                table: "Attachments",
                column: "UploadedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentContents");

            migrationBuilder.DropTable(
                name: "Attachments");
        }
    }
}
