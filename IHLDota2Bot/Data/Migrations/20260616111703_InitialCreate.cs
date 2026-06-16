using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IHLDota2Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallengerUserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ChallengedUserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultingMatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LobbyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WinningTeam = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    UserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SteamId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false, defaultValue: 1000),
                    Wins = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Losses = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsBanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BanReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayers",
                columns: table => new
                {
                    MatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    Team = table.Column<int>(type: "int", nullable: false),
                    Won = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayers", x => new { x.MatchId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Players_UserId",
                        column: x => x.UserId,
                        principalTable: "Players",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ChallengedUserId_Status",
                table: "Challenges",
                columns: new[] { "ChallengedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ChallengerUserId_Status",
                table: "Challenges",
                columns: new[] { "ChallengerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ExpiresAt",
                table: "Challenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompletedAt",
                table: "Matches",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_Status",
                table: "Matches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_UserId",
                table: "MatchPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SteamId",
                table: "Players",
                column: "SteamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Challenges");

            migrationBuilder.DropTable(
                name: "MatchPlayers");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
