using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chronicles.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261025230000_RemoveUnusedStarterSystems")]
public sealed class RemoveUnusedStarterSystems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM rpg_systems AS system
            WHERE system."OwnerId" IS NULL
              AND system."Id" IN (
                'a9e29682-c194-4aae-9c0e-7bbb3f6a72f1',
                'b63b9373-858c-49cd-90b6-c9301ed804eb',
                '8771d4d1-2c93-49d7-9ffc-8244ea0be2b9'
              )
              AND NOT EXISTS (SELECT 1 FROM campaigns AS campaign WHERE campaign."SystemId" = system."Id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
