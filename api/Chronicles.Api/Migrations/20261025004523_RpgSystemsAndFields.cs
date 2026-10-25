using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class RpgSystemsAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FieldsJson",
                table: "characters",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "SystemId",
                table: "campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rpg_systems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SchemaJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpg_systems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpg_systems_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_SystemId",
                table: "campaigns",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_rpg_systems_OwnerId_Name",
                table: "rpg_systems",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_campaigns_rpg_systems_SystemId",
                table: "campaigns",
                column: "SystemId",
                principalTable: "rpg_systems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaigns_rpg_systems_SystemId",
                table: "campaigns");

            migrationBuilder.DropTable(
                name: "rpg_systems");

            migrationBuilder.DropIndex(
                name: "IX_campaigns_SystemId",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "FieldsJson",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "SystemId",
                table: "campaigns");
        }
    }
}
