using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMembership> CampaignMemberships => Set<CampaignMembership>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<RoomParticipant> RoomParticipants => Set<RoomParticipant>();
    public DbSet<CampaignInvite> CampaignInvites => Set<CampaignInvite>();
    public DbSet<NarrativeEntry> NarrativeEntries => Set<NarrativeEntry>();
    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();
    public DbSet<RpgSystem> RpgSystems => Set<RpgSystem>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<WorldArticle> WorldArticles => Set<WorldArticle>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.DisplayName).HasMaxLength(80);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Bio).HasMaxLength(1000);
            entity.Property(x => x.Accent).HasMaxLength(20).HasDefaultValue("gold");
        });
        model.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PasswordResetCode>(entity =>
        {
            entity.ToTable("password_reset_codes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.Property(x => x.CodeHash).HasMaxLength(64);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Campaign>(entity =>
        {
            entity.ToTable("campaigns");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UpdatedAt);
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.AccentColor).HasMaxLength(7);
            entity.HasOne(x => x.System).WithMany().HasForeignKey(x => x.SystemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.World).WithMany().HasForeignKey(x => x.WorldId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CampaignMembership>(entity =>
        {
            entity.ToTable("campaign_memberships");
            entity.HasKey(x => new { x.CampaignId, x.UserId });
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany(x => x.Memberships).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Character>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.Name });
            entity.HasIndex(x => x.OverlayKeyHash).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.PortraitUrl).HasMaxLength(2048);
            entity.Property(x => x.BackgroundUrl).HasMaxLength(2048);
            entity.Property(x => x.OverlayKeyHash).HasMaxLength(64);
            entity.Property(x => x.FieldsJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RoomParticipant>(entity =>
        {
            entity.ToTable("room_participants");
            entity.HasKey(x => new { x.CampaignId, x.UserId });
            entity.HasIndex(x => x.RealtimeParticipantId).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CampaignInvite>(entity =>
        {
            entity.ToTable("campaign_invites");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.CampaignId, x.ExpiresAt });
            entity.Property(x => x.TokenHash).HasMaxLength(64);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UsedById).OnDelete(DeleteBehavior.SetNull);
        });
        model.Entity<NarrativeEntry>(entity =>
        {
            entity.ToTable("narrative_entries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.Kind, x.UpdatedAt });
            entity.HasIndex(x => new { x.CampaignId, x.Kind, x.Title });
            entity.Property(x => x.Kind).HasMaxLength(20);
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(40000);
            entity.Property(x => x.Category).HasMaxLength(40);
            entity.Property(x => x.Visibility).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DiceRoll>(entity =>
        {
            entity.ToTable("dice_rolls");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.CreatedAt });
            entity.HasIndex(x => new { x.CampaignId, x.UserId, x.RequestId }).IsUnique();
            entity.Property(x => x.Expression).HasMaxLength(40);
            entity.Property(x => x.RollsJson).HasMaxLength(300);
            entity.Property(x => x.KeptIndexesJson).HasMaxLength(300);
            entity.Property(x => x.Visibility).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RpgSystem>(entity =>
        {
            entity.ToTable("rpg_systems");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.SchemaJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<World>(entity =>
        {
            entity.ToTable("worlds");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OwnerId, x.UpdatedAt });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorldArticle>(entity =>
        {
            entity.ToTable("world_articles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.WorldId, x.Category, x.Title });
            entity.HasIndex(x => new { x.WorldId, x.UpdatedAt });
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Body).HasMaxLength(40000);
            entity.Property(x => x.Category).HasMaxLength(40);
            entity.Property(x => x.Visibility).HasMaxLength(20);
            entity.HasOne(x => x.World).WithMany().HasForeignKey(x => x.WorldId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
