using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    public partial class RemoveShadowGameId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Drop Players.GameId1 if it slipped in
IF COL_LENGTH('Players', 'GameId1') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Players_GameId1' AND object_id = OBJECT_ID('Players'))
        DROP INDEX [IX_Players_GameId1] ON [Players];
    ALTER TABLE [Players] DROP COLUMN [GameId1];
END

-- Drop Eliminations.GameId1 and its FK/Index if present
IF COL_LENGTH('Eliminations', 'GameId1') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Games_GameId1')
        ALTER TABLE [Eliminations] DROP CONSTRAINT [FK_Eliminations_Games_GameId1];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_GameId1' AND object_id = OBJECT_ID('Eliminations'))
        DROP INDEX [IX_Eliminations_GameId1] ON [Eliminations];
    ALTER TABLE [Eliminations] DROP COLUMN [GameId1];
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — we never want GameId1 back.
        }
    }
}
