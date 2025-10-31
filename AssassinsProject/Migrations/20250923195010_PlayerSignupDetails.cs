using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    /// <inheritdoc />
    public partial class PlayerSignupDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApproximateAge",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EyeColor",
                table: "Players",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HairColor",
                table: "Players",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealName",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Specialty",
                table: "Players",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisibleMarkings",
                table: "Players",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ApproximateAge",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EyeColor",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HairColor",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "RealName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Specialty",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "VisibleMarkings",
                table: "Players");
        }
    }
}
