using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    public partial class AlignDateTimesToOffset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
            migrationBuilder.Sql(@"
DECLARE @sql NVARCHAR(MAX) = N'';

-- Helper to drop default constraint if exists
WITH d AS (
    SELECT t.[name] AS TableName, c.[name] AS ColumnName, dc.name AS DefaultName
    FROM sys.tables t
    JOIN sys.columns c ON c.object_id = t.object_id
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id
    WHERE t.[name] IN (N'Players', N'Games', N'Eliminations')
      AND c.[name] IN (N'PasscodeSetAt', N'VerificationSentAt', N'StartedAt', N'EndedAt', N'OccurredAt', N'VerifiedAt')
      AND dc.[name] IS NOT NULL
)
SELECT @sql = STRING_AGG(CONCAT(N'ALTER TABLE [', TableName, N'] DROP CONSTRAINT [', DefaultName, N'];'), CHAR(10))
FROM d;

IF @sql IS NOT NULL AND LEN(@sql) > 0
    EXEC sp_executesql @sql;
");

            // --- Players ---
            migrationBuilder.Sql(@"
IF COL_LENGTH('Players','PasscodeSetAt') IS NOT NULL
    ALTER TABLE [Players] ALTER COLUMN [PasscodeSetAt] datetimeoffset(7) NOT NULL;
IF COL_LENGTH('Players','VerificationSentAt') IS NOT NULL
    ALTER TABLE [Players] ALTER COLUMN [VerificationSentAt] datetimeoffset(7) NULL;
");

            // --- Games ---
            migrationBuilder.Sql(@"
IF COL_LENGTH('Games','StartedAt') IS NOT NULL
    ALTER TABLE [Games] ALTER COLUMN [StartedAt] datetimeoffset(7) NULL;
IF COL_LENGTH('Games','EndedAt') IS NOT NULL
    ALTER TABLE [Games] ALTER COLUMN [EndedAt] datetimeoffset(7) NULL;
");

            // --- Eliminations ---
            migrationBuilder.Sql(@"
IF COL_LENGTH('Eliminations','OccurredAt') IS NOT NULL
    ALTER TABLE [Eliminations] ALTER COLUMN [OccurredAt] datetimeoffset(7) NOT NULL;
IF COL_LENGTH('Eliminations','VerifiedAt') IS NOT NULL
    ALTER TABLE [Eliminations] ALTER COLUMN [VerifiedAt] datetimeoffset(7) NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert back to datetime2 if you ever need to roll back.
            // (Adjust if your original type was datetime)
            migrationBuilder.Sql(@"
IF COL_LENGTH('Players','PasscodeSetAt') IS NOT NULL
    ALTER TABLE [Players] ALTER COLUMN [PasscodeSetAt] datetime2(7) NOT NULL;
IF COL_LENGTH('Players','VerificationSentAt') IS NOT NULL
    ALTER TABLE [Players] ALTER COLUMN [VerificationSentAt] datetime2(7) NULL;

IF COL_LENGTH('Games','StartedAt') IS NOT NULL
    ALTER TABLE [Games] ALTER COLUMN [StartedAt] datetime2(7) NULL;
IF COL_LENGTH('Games','EndedAt') IS NOT NULL
    ALTER TABLE [Games] ALTER COLUMN [EndedAt] datetime2(7) NULL;

IF COL_LENGTH('Eliminations','OccurredAt') IS NOT NULL
    ALTER TABLE [Eliminations] ALTER COLUMN [OccurredAt] datetime2(7) NOT NULL;
IF COL_LENGTH('Eliminations','VerifiedAt') IS NOT NULL
    ALTER TABLE [Eliminations] ALTER COLUMN [VerifiedAt] datetime2(7) NULL;
");
        }
    }
}
