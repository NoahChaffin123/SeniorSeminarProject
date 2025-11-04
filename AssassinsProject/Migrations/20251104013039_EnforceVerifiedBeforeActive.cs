using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    public partial class EnforceVerifiedBeforeActive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE p
SET IsActive = 0
FROM Players p
WHERE p.IsEmailVerified = 0 AND p.IsActive = 1;
");


            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Players_ActiveRequiresVerified'
)
ALTER TABLE [Players]
ADD CONSTRAINT [CK_Players_ActiveRequiresVerified]
CHECK (CASE WHEN [IsActive] = 1 THEN [IsEmailVerified] ELSE 1 END = 1);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Players_ActiveRequiresVerified'
)
ALTER TABLE [Players] DROP CONSTRAINT [CK_Players_ActiveRequiresVerified];
");
        }
    }
}
