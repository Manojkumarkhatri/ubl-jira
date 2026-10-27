using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserRightsAndSpaceTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The new ticks join each type's defaults without undoing an Admin's changes, and every type keeps within its
            // limit (owner, 2026-10-26): Heads get adding Portfolio leads, deleting and account management; Portfolio leads
            // deleting work items; Members deleting their own.
            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = 131071 WHERE Type = 'Admin';");
            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = (Rights | 124928) & 126911 WHERE Type = 'Head';");
            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = (Rights | 24576) & 59295 WHERE Type = 'PortfolioLead';");
            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = (Rights | 8192) & 24715 WHERE Type = 'Member';");
            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = Rights & 136 WHERE Type = 'ReadOnly';");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletedById",
                table: "Projects",
                column: "DeletedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_DeletedById",
                table: "Projects",
                column: "DeletedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_DeletedById",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DeletedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Projects");

            migrationBuilder.Sql("UPDATE UserTypeRights SET Rights = Rights & 2047;");
        }
    }
}
