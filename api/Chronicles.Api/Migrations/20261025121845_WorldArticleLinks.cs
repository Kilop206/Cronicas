using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class WorldArticleLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_article_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_article_links", x => x.Id);
                    table.CheckConstraint("CK_world_article_links_distinct", "\"SourceId\" <> \"TargetId\"");
                    table.ForeignKey(
                        name: "FK_world_article_links_world_articles_SourceId",
                        column: x => x.SourceId,
                        principalTable: "world_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_world_article_links_world_articles_TargetId",
                        column: x => x.TargetId,
                        principalTable: "world_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_world_article_links_SourceId_TargetId_Label",
                table: "world_article_links",
                columns: new[] { "SourceId", "TargetId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_world_article_links_TargetId",
                table: "world_article_links",
                column: "TargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_article_links");
        }
    }
}
