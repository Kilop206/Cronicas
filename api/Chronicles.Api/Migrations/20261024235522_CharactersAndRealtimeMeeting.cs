using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class CharactersAndRealtimeMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RealtimeMeetingId",
                table: "campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PortraitUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BackgroundUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    Sanity = table.Column<int>(type: "integer", nullable: false),
                    MaxSanity = table.Column<int>(type: "integer", nullable: false),
                    Mana = table.Column<int>(type: "integer", nullable: false),
                    MaxMana = table.Column<int>(type: "integer", nullable: false),
                    OverlayKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_characters_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_characters_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_characters_CampaignId_Name",
                table: "characters",
                columns: new[] { "CampaignId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_characters_OverlayKeyHash",
                table: "characters",
                column: "OverlayKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_OwnerId",
                table: "characters",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropColumn(
                name: "RealtimeMeetingId",
                table: "campaigns");
        }
    }
}
