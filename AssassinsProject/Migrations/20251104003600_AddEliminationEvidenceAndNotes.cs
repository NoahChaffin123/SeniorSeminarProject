using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    public partial class AddEliminationEvidenceAndNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add EvidenceUrl (nullable)
            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrl",
                table: "Eliminations",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            // Add Notes (nullable)
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Eliminations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceUrl",
                table: "Eliminations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Eliminations");
        }
    }
}
