using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class DiceRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dice_rolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Expression = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RollsJson = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    KeptIndexesJson = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Modifier = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dice_rolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dice_rolls_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dice_rolls_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dice_rolls_CampaignId_CreatedAt",
                table: "dice_rolls",
                columns: new[] { "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_dice_rolls_CampaignId_UserId_RequestId",
                table: "dice_rolls",
                columns: new[] { "CampaignId", "UserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dice_rolls_UserId",
                table: "dice_rolls",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dice_rolls");
        }
    }
}
