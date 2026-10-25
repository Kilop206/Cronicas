using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class WorldsAndArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorldId",
                table: "campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "worlds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worlds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_worlds_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "world_articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorldId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_world_articles_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_world_articles_worlds_WorldId",
                        column: x => x.WorldId,
                        principalTable: "worlds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_WorldId",
                table: "campaigns",
                column: "WorldId");

            migrationBuilder.CreateIndex(
                name: "IX_world_articles_AuthorId",
                table: "world_articles",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_world_articles_WorldId_Category_Title",
                table: "world_articles",
                columns: new[] { "WorldId", "Category", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_world_articles_WorldId_UpdatedAt",
                table: "world_articles",
                columns: new[] { "WorldId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_worlds_OwnerId_UpdatedAt",
                table: "worlds",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_campaigns_worlds_WorldId",
                table: "campaigns",
                column: "WorldId",
                principalTable: "worlds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaigns_worlds_WorldId",
                table: "campaigns");

            migrationBuilder.DropTable(
                name: "world_articles");

            migrationBuilder.DropTable(
                name: "worlds");

            migrationBuilder.DropIndex(
                name: "IX_campaigns_WorldId",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "WorldId",
                table: "campaigns");
        }
    }
}
