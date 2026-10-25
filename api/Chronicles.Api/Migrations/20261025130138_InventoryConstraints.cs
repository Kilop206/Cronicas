using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronicles.Api.Migrations
{
    /// <inheritdoc />
    public partial class InventoryConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_category",
                table: "character_inventory_items",
                sql: "\"Category\" IN ('item','equipment','weapon','consumable','other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_quantity",
                table: "character_inventory_items",
                sql: "\"Quantity\" BETWEEN 1 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "CK_inventory_weight",
                table: "character_inventory_items",
                sql: "\"Weight\" BETWEEN 0 AND 9999.99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_category",
                table: "character_inventory_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_quantity",
                table: "character_inventory_items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_inventory_weight",
                table: "character_inventory_items");
        }
    }
}
