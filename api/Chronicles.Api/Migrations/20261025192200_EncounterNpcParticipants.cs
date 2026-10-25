using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class EncounterNpcParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                table: "encounter_participants",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "NpcId",
                table: "encounter_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_EncounterId_NpcId",
                table: "encounter_participants",
                columns: new[] { "EncounterId", "NpcId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_NpcId",
                table: "encounter_participants",
                column: "NpcId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_encounter_actor",
                table: "encounter_participants",
                sql: "(\"CharacterId\" IS NOT NULL AND \"NpcId\" IS NULL) OR (\"CharacterId\" IS NULL AND \"NpcId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_encounter_participants_npcs_NpcId",
                table: "encounter_participants",
                column: "NpcId",
                principalTable: "npcs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_encounter_participants_npcs_NpcId",
                table: "encounter_participants");

            migrationBuilder.DropIndex(
                name: "IX_encounter_participants_EncounterId_NpcId",
                table: "encounter_participants");

            migrationBuilder.DropIndex(
                name: "IX_encounter_participants_NpcId",
                table: "encounter_participants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_encounter_actor",
                table: "encounter_participants");

            migrationBuilder.DropColumn(
                name: "NpcId",
                table: "encounter_participants");

            migrationBuilder.AlterColumn<Guid>(
                name: "CharacterId",
                table: "encounter_participants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
