using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Upms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserTypesAndPortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PortfolioId",
                table: "Projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FromPortfolio",
                table: "ProjectMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "DepartmentId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RightsDenied",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "RightsGranted",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "AspNetUsers",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTypeRights",
                columns: table => new
                {
                    Type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Rights = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTypeRights", x => x.Type);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Portfolios_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioLeads",
                columns: table => new
                {
                    PortfolioId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioLeads", x => new { x.PortfolioId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PortfolioLeads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortfolioLeads_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "UserTypeRights",
                columns: new[] { "Type", "Rights" },
                values: new object[,]
                {
                    { "Admin", 2047L },
                    { "Head", 1983L },
                    { "Member", 137L },
                    { "PortfolioLead", 415L },
                    { "ReadOnly", 8L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PortfolioId",
                table: "Projects",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DepartmentId",
                table: "AspNetUsers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_NormalizedName",
                table: "Departments",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioLeads_UserId",
                table: "PortfolioLeads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_DepartmentId_NormalizedName",
                table: "Portfolios",
                columns: new[] { "DepartmentId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Departments_DepartmentId",
                table: "AspNetUsers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Portfolios_PortfolioId",
                table: "Projects",
                column: "PortfolioId",
                principalTable: "Portfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Existing accounts get their type (owner's decision of 2026-10-20): administrators are Admins; the Space admins
            // of any space are Portfolio leads (a space lead is a portfolio lead); people who only view spaces are Read-only.
            // Their roles in spaces stay as they are until their type or ticks change.
            migrationBuilder.Sql("""
                UPDATE AspNetUsers SET UserType = 'Admin' WHERE OrganizationRole = 'Administrator';
                UPDATE u SET UserType = 'PortfolioLead' FROM AspNetUsers u
                WHERE u.OrganizationRole <> 'Administrator'
                  AND EXISTS (SELECT 1 FROM ProjectMembers m WHERE m.UserId = u.Id AND m.Role = 'ProjectAdmin');
                UPDATE u SET UserType = 'ReadOnly' FROM AspNetUsers u
                WHERE u.OrganizationRole <> 'Administrator'
                  AND EXISTS (SELECT 1 FROM ProjectMembers m WHERE m.UserId = u.Id)
                  AND NOT EXISTS (SELECT 1 FROM ProjectMembers m WHERE m.UserId = u.Id AND m.Role <> 'Viewer');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Departments_DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Portfolios_PortfolioId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "PortfolioLeads");

            migrationBuilder.DropTable(
                name: "UserTypeRights");

            migrationBuilder.DropTable(
                name: "Portfolios");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Projects_PortfolioId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PortfolioId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "FromPortfolio",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RightsDenied",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RightsGranted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AspNetUsers");
        }
    }
}
