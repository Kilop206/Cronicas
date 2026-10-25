using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class Npcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PortraitUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Appearance = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Personality = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Motivation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Relationships = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Secrets = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npcs", x => x.Id);
                    table.CheckConstraint("CK_npc_health", "\"Health\" >= 0 AND \"Health\" <= \"MaxHealth\" AND \"MaxHealth\" BETWEEN 1 AND 9999");
                    table.CheckConstraint("CK_npc_status", "\"Status\" IN ('active','missing','deceased','unknown')");
                    table.CheckConstraint("CK_npc_visibility", "\"Visibility\" IN ('gm','campaign')");
                    table.ForeignKey(
                        name: "FK_npcs_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_npcs_CampaignId_Name",
                table: "npcs",
                columns: new[] { "CampaignId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "npcs");
        }
    }
}
