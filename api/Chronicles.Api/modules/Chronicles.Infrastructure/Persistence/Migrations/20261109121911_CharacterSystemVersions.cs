using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class CharacterSystemVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SystemVersionId",
                table: "characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rpg_system_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RpgSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpg_system_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpg_system_versions_rpg_systems_RpgSystemId",
                        column: x => x.RpgSystemId,
                        principalTable: "rpg_systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_characters_SystemVersionId",
                table: "characters",
                column: "SystemVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_rpg_system_versions_RpgSystemId_ContentHash",
                table: "rpg_system_versions",
                columns: new[] { "RpgSystemId", "ContentHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_characters_rpg_system_versions_SystemVersionId",
                table: "characters",
                column: "SystemVersionId",
                principalTable: "rpg_system_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION cronicas_immutable_system_version() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Captured RPG system versions are immutable.' USING ERRCODE = 'P0001';
                END;
                $$;
                CREATE TRIGGER immutable_system_version BEFORE UPDATE OR DELETE ON rpg_system_versions
                FOR EACH ROW EXECUTE FUNCTION cronicas_immutable_system_version();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM rpg_system_versions) OR EXISTS (SELECT 1 FROM characters WHERE "SystemVersionId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove captured system versions. Preserve the additive schema and roll back the application only.' USING ERRCODE = 'P0001';
                    END IF;
                END $$;
                DROP TRIGGER immutable_system_version ON rpg_system_versions;
                DROP FUNCTION cronicas_immutable_system_version();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_characters_rpg_system_versions_SystemVersionId",
                table: "characters");

            migrationBuilder.DropTable(
                name: "rpg_system_versions");

            migrationBuilder.DropIndex(
                name: "IX_characters_SystemVersionId",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "SystemVersionId",
                table: "characters");
        }
    }
}
