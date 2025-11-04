using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    /// <inheritdoc />
    public partial class FixEliminationFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_GameId_EliminatorEmail')
    ALTER TABLE [Eliminations] DROP CONSTRAINT [FK_Eliminations_Players_GameId_EliminatorEmail];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_GameId_VictimEmail')
    ALTER TABLE [Eliminations] DROP CONSTRAINT [FK_Eliminations_Players_GameId_VictimEmail];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_GameId' AND object_id = OBJECT_ID('[Eliminations]'))
    DROP INDEX [IX_Eliminations_GameId] ON [Eliminations];

-- Align Notes column type if it exists
IF COL_LENGTH('Eliminations', 'Notes') IS NOT NULL
    ALTER TABLE [Eliminations] ALTER COLUMN [Notes] nvarchar(max) NULL;

-- *** Bound the email columns so they can be indexed / referenced ***
IF COL_LENGTH('Eliminations', 'EliminatorEmail') IS NOT NULL
BEGIN
    -- trim values that are too long, just in case
    UPDATE [Eliminations]
    SET [EliminatorEmail] = LEFT([EliminatorEmail], 256)
    WHERE LEN([EliminatorEmail]) > 256;

    ALTER TABLE [Eliminations] ALTER COLUMN [EliminatorEmail] nvarchar(256) NOT NULL;
END

IF COL_LENGTH('Eliminations', 'VictimEmail') IS NOT NULL
BEGIN
    UPDATE [Eliminations]
    SET [VictimEmail] = LEFT([VictimEmail], 256)
    WHERE LEN([VictimEmail]) > 256;

    ALTER TABLE [Eliminations] ALTER COLUMN [VictimEmail] nvarchar(256) NOT NULL;
END

-- Add new scoped GameId columns if missing
IF COL_LENGTH('Eliminations', 'EliminatorGameId') IS NULL
    ALTER TABLE [Eliminations] ADD [EliminatorGameId] int NOT NULL CONSTRAINT DF_Elims_EliminatorGameId DEFAULT 0;

IF COL_LENGTH('Eliminations', 'VictimGameId') IS NULL
    ALTER TABLE [Eliminations] ADD [VictimGameId] int NOT NULL CONSTRAINT DF_Elims_VictimGameId DEFAULT 0;

-- Create composite indexes if missing
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_EliminatorGameId_EliminatorEmail' AND object_id = OBJECT_ID('[Eliminations]'))
    CREATE INDEX [IX_Eliminations_EliminatorGameId_EliminatorEmail] ON [Eliminations]([EliminatorGameId],[EliminatorEmail]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_VictimGameId_VictimEmail' AND object_id = OBJECT_ID('[Eliminations]'))
    CREATE INDEX [IX_Eliminations_VictimGameId_VictimEmail] ON [Eliminations]([VictimGameId],[VictimEmail]);

-- New FKs to Players(GameId, Email) (NO ACTION is default in SQL Server unless ON DELETE is specified)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_EliminatorGameId_EliminatorEmail')
    ALTER TABLE [Eliminations] ADD CONSTRAINT [FK_Eliminations_Players_EliminatorGameId_EliminatorEmail]
        FOREIGN KEY ([EliminatorGameId],[EliminatorEmail]) REFERENCES [Players]([GameId],[Email]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_VictimGameId_VictimEmail')
    ALTER TABLE [Eliminations] ADD CONSTRAINT [FK_Eliminations_Players_VictimGameId_VictimEmail]
        FOREIGN KEY ([VictimGameId],[VictimEmail]) REFERENCES [Players]([GameId],[Email]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_EliminatorGameId_EliminatorEmail')
    ALTER TABLE [Eliminations] DROP CONSTRAINT [FK_Eliminations_Players_EliminatorGameId_EliminatorEmail];

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Eliminations_Players_VictimGameId_VictimEmail')
    ALTER TABLE [Eliminations] DROP CONSTRAINT [FK_Eliminations_Players_VictimGameId_VictimEmail];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_EliminatorGameId_EliminatorEmail' AND object_id = OBJECT_ID('[Eliminations]'))
    DROP INDEX [IX_Eliminations_EliminatorGameId_EliminatorEmail] ON [Eliminations];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eliminations_VictimGameId_VictimEmail' AND object_id = OBJECT_ID('[Eliminations]'))
    DROP INDEX [IX_Eliminations_VictimGameId_VictimEmail] ON [Eliminations];

IF COL_LENGTH('Eliminations', 'EliminatorGameId') IS NOT NULL
    ALTER TABLE [Eliminations] DROP CONSTRAINT DF_Elims_EliminatorGameId;

IF COL_LENGTH('Eliminations', 'VictimGameId') IS NOT NULL
    ALTER TABLE [Eliminations] DROP CONSTRAINT DF_Elims_VictimGameId;

IF COL_LENGTH('Eliminations', 'EliminatorGameId') IS NOT NULL
    ALTER TABLE [Eliminations] DROP COLUMN [EliminatorGameId];

IF COL_LENGTH('Eliminations', 'VictimGameId') IS NOT NULL
    ALTER TABLE [Eliminations] DROP COLUMN [VictimGameId];

-- (Optional) revert email column sizes if you truly need to
-- ALTER TABLE [Eliminations] ALTER COLUMN [EliminatorEmail] nvarchar(max) NOT NULL;
-- ALTER TABLE [Eliminations] ALTER COLUMN [VictimEmail] nvarchar(max) NOT NULL;

-- (Optional) recreate the old single-column index if you really want the prior state
-- CREATE INDEX [IX_Eliminations_GameId] ON [Eliminations]([GameId]);
");
        }
    }
}
