using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    public partial class UniqueAliasPerGame : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some projects had a unique index on (GameId, DisplayName). Drop it if it exists.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Players_GameId_DisplayName' AND object_id = OBJECT_ID('dbo.Players'))
    DROP INDEX [IX_Players_GameId_DisplayName] ON [dbo].[Players];
");

            // 1) Trim whitespace from Alias
            migrationBuilder.Sql(@"
UPDATE p
SET Alias = LTRIM(RTRIM(Alias))
FROM dbo.Players p;
");

            // 2) Backfill NULL/empty aliases from DisplayName, then email local-part
            migrationBuilder.Sql(@"
UPDATE p
SET Alias = COALESCE(
    NULLIF(LTRIM(RTRIM(p.Alias)), ''),
    NULLIF(LTRIM(RTRIM(p.DisplayName)), ''),
    CASE 
        WHEN CHARINDEX('@', p.Email) > 1 THEN LEFT(p.Email, CHARINDEX('@', p.Email) - 1)
        ELSE p.Email
    END
)
FROM dbo.Players p
WHERE p.Alias IS NULL OR LTRIM(RTRIM(p.Alias)) = '';
");

            // 3) Deduplicate within each game: for duplicates keep first, append _1, _2, ... to the rest
            migrationBuilder.Sql(@"
;WITH d AS (
    SELECT 
        GameId,
        Email,
        Alias,
        ROW_NUMBER() OVER (PARTITION BY GameId, Alias ORDER BY Email) AS rn
    FROM dbo.Players
)
UPDATE d
SET Alias = CONCAT(Alias, '_', rn - 1)
WHERE rn > 1;
");

            // 4) Finally, create the unique index on (GameId, Alias)
            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId_Alias",
                table: "Players",
                columns: new[] { "GameId", "Alias" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_GameId_Alias",
                table: "Players");

            // (Optional) restore the old unique index on DisplayName if your earlier schema had it
            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId_DisplayName",
                table: "Players",
                columns: new[] { "GameId", "DisplayName" },
                unique: true);
        }
    }
}
