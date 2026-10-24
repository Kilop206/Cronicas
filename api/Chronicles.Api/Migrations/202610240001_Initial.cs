using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Chronicles.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202610240001_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("users", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
            PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_users", x => x.Id));
        migrationBuilder.CreateTable("campaigns", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
            OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_campaigns", x => x.Id);
            table.ForeignKey("FK_campaigns_users_OwnerId", x => x.OwnerId, "users", "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("user_sessions", table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_user_sessions", x => x.Id);
            table.ForeignKey("FK_user_sessions_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateTable("campaign_memberships", table => new
        {
            CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
            UserId = table.Column<Guid>(type: "uuid", nullable: false),
            Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_campaign_memberships", x => new { x.CampaignId, x.UserId });
            table.ForeignKey("FK_campaign_memberships_campaigns_CampaignId", x => x.CampaignId, "campaigns", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_campaign_memberships_users_UserId", x => x.UserId, "users", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_users_Email", "users", "Email", unique: true);
        migrationBuilder.CreateIndex("IX_campaigns_OwnerId", "campaigns", "OwnerId");
        migrationBuilder.CreateIndex("IX_campaigns_UpdatedAt", "campaigns", "UpdatedAt");
        migrationBuilder.CreateIndex("IX_user_sessions_TokenHash", "user_sessions", "TokenHash", unique: true);
        migrationBuilder.CreateIndex("IX_user_sessions_UserId_ExpiresAt", "user_sessions", new[] { "UserId", "ExpiresAt" });
        migrationBuilder.CreateIndex("IX_campaign_memberships_UserId", "campaign_memberships", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("campaign_memberships");
        migrationBuilder.DropTable("user_sessions");
        migrationBuilder.DropTable("campaigns");
        migrationBuilder.DropTable("users");
    }
}
