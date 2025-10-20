using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    /// <inheritdoc />
    public partial class AdminFullEditAndPasscodeView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasscodePlaintext",
                table: "Players",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasscodePlaintext",
                table: "Players");
        }
    }
}
