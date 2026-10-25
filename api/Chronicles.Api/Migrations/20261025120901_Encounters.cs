using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class Encounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "encounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    TurnIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_encounters_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Initiative = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    Conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_encounter_participants_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_encounter_participants_encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_CharacterId",
                table: "encounter_participants",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_EncounterId_CharacterId",
                table: "encounter_participants",
                columns: new[] { "EncounterId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_encounters_CampaignId_UpdatedAt",
                table: "encounters",
                columns: new[] { "CampaignId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encounter_participants");

            migrationBuilder.DropTable(
                name: "encounters");
        }
    }
}
