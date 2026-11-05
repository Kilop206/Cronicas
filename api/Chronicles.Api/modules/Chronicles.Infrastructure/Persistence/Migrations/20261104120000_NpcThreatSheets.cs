using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chronicles.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261104120000_NpcThreatSheets")]
public sealed class NpcThreatSheets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name:"SheetJson",table:"npcs",type:"jsonb",nullable:true);
        migrationBuilder.AddColumn<string>(name:"HistoryJson",table:"npcs",type:"jsonb",nullable:false,defaultValue:"[]");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name:"SheetJson",table:"npcs");
        migrationBuilder.DropColumn(name:"HistoryJson",table:"npcs");
    }
}
