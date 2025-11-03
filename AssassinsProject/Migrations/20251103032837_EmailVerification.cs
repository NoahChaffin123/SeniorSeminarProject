using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAt",
                table: "Players",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerifyToken",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifyTokenExpiresAt",
                table: "Players",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EmailVerifyToken",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EmailVerifyTokenExpiresAt",
                table: "Players");
        }
    }
}
