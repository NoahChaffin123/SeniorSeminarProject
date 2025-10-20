using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssassinsProject.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Eliminations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    EliminatorEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VictimEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    PasscodeVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eliminations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Eliminations_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EmailNormalized = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    TargetEmail = table.Column<string>(type: "nvarchar(256)", nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotoContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotoBytesSha256 = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PasscodeHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PasscodeSalt = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PasscodeAlgo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasscodeCost = table.Column<int>(type: "int", nullable: false),
                    PasscodeSetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => new { x.GameId, x.Email });
                    table.ForeignKey(
                        name: "FK_Players_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Players_Players_GameId_TargetEmail",
                        columns: x => new { x.GameId, x.TargetEmail },
                        principalTable: "Players",
                        principalColumns: new[] { "GameId", "Email" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Eliminations_GameId",
                table: "Eliminations",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId_DisplayName",
                table: "Players",
                columns: new[] { "GameId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId_EmailNormalized",
                table: "Players",
                columns: new[] { "GameId", "EmailNormalized" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_GameId_TargetEmail",
                table: "Players",
                columns: new[] { "GameId", "TargetEmail" },
                unique: true,
                filter: "[IsActive] = 1 AND [TargetEmail] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Eliminations");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
