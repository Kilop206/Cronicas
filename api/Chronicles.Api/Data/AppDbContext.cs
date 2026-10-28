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
    public DbSet<CharacterSecret> CharacterSecrets => Set<CharacterSecret>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<CharacterFavorite> CharacterFavorites => Set<CharacterFavorite>();
    public DbSet<CharacterInventoryItem> CharacterInventoryItems => Set<CharacterInventoryItem>();
    public DbSet<CharacterAbility> CharacterAbilities => Set<CharacterAbility>();
    public DbSet<CampaignInvite> CampaignInvites => Set<CampaignInvite>();
    public DbSet<NarrativeEntry> NarrativeEntries => Set<NarrativeEntry>();
    public DbSet<DiceRoll> DiceRolls => Set<DiceRoll>();
    public DbSet<RpgSystem> RpgSystems => Set<RpgSystem>();
    public DbSet<World> Worlds => Set<World>();
    public DbSet<WorldArticle> WorldArticles => Set<WorldArticle>();
    public DbSet<WorldArticleLink> WorldArticleLinks => Set<WorldArticleLink>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();
    public DbSet<CharacterRevision> CharacterRevisions => Set<CharacterRevision>();

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
            entity.Property(x => x.AvatarUrl).HasMaxLength(2048);
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
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CharacterSecret>(entity =>
        {
            entity.ToTable("character_secrets");
            entity.HasKey(x => x.CharacterId);
            entity.Property(x => x.Notes).HasMaxLength(10000);
            entity.Property(x => x.Revision).IsConcurrencyToken();
            entity.HasOne(x => x.Character).WithOne().HasForeignKey<CharacterSecret>(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Npc>(entity =>
        {
            entity.ToTable("npcs", table =>
            {
                table.HasCheckConstraint("CK_npc_health", "\"Health\" >= 0 AND \"Health\" <= \"MaxHealth\" AND \"MaxHealth\" BETWEEN 1 AND 9999");
                table.HasCheckConstraint("CK_npc_visibility", "\"Visibility\" IN ('gm','campaign')");
                table.HasCheckConstraint("CK_npc_status", "\"Status\" IN ('active','missing','deceased','unknown')");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.PortraitUrl).HasMaxLength(2048);
            entity.Property(x => x.Appearance).HasMaxLength(2000);
            entity.Property(x => x.Personality).HasMaxLength(2000);
            entity.Property(x => x.Motivation).HasMaxLength(2000);
            entity.Property(x => x.Relationships).HasMaxLength(2000);
            entity.Property(x => x.Location).HasMaxLength(160);
            entity.Property(x => x.Secrets).HasMaxLength(10000);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.Visibility).HasMaxLength(20);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CharacterFavorite>(entity =>
        {
            entity.ToTable("character_favorites");
            entity.HasKey(x => new { x.UserId, x.CharacterId });
            entity.HasIndex(x => x.CharacterId);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CharacterInventoryItem>(entity =>
        {
            entity.ToTable("character_inventory_items", table =>
            {
                table.HasCheckConstraint("CK_inventory_quantity", "\"Quantity\" BETWEEN 1 AND 9999");
                table.HasCheckConstraint("CK_inventory_weight", "\"Weight\" BETWEEN 0 AND 9999.99");
                table.HasCheckConstraint("CK_inventory_category", "\"Category\" IN ('item','equipment','weapon','consumable','other')");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CharacterId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Category).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Weight).HasPrecision(10, 2);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CharacterAbility>(entity =>
        {
            entity.ToTable("character_abilities");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CharacterId, x.Name });
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Cost).HasMaxLength(80);
            entity.Property(x => x.Effect).HasMaxLength(4000);
            entity.Property(x => x.Duration).HasMaxLength(80);
            entity.Property(x => x.Limitations).HasMaxLength(1000);
            entity.Property(x => x.Recharge).HasMaxLength(80);
            entity.Property(x => x.RollExpression).HasMaxLength(40);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(x => x.SchemaJson).HasColumnType("jsonb").IsConcurrencyToken();
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
        model.Entity<WorldArticleLink>(entity =>
        {
            entity.ToTable("world_article_links", table => table.HasCheckConstraint("CK_world_article_links_distinct", "\"SourceId\" <> \"TargetId\""));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SourceId, x.TargetId, x.Label }).IsUnique();
            entity.HasIndex(x => x.TargetId);
            entity.Property(x => x.Label).HasMaxLength(80);
            entity.HasOne(x => x.Source).WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Target).WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.CreatedAt });
            entity.HasIndex(x => new { x.CampaignId, x.UserId, x.ClientId }).IsUnique();
            entity.Property(x => x.Body).HasMaxLength(2000);
            entity.Property(x => x.Kind).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Encounter>(entity =>
        {
            entity.ToTable("encounters");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampaignId, x.UpdatedAt });
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<EncounterParticipant>(entity =>
        {
            entity.ToTable("encounter_participants", table => table.HasCheckConstraint("CK_encounter_actor", "(\"CharacterId\" IS NOT NULL AND \"NpcId\" IS NULL) OR (\"CharacterId\" IS NULL AND \"NpcId\" IS NOT NULL)"));
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EncounterId, x.CharacterId }).IsUnique();
            entity.HasIndex(x => new { x.EncounterId, x.NpcId }).IsUnique();
            entity.Property(x => x.Conditions).HasMaxLength(1000);
            entity.HasOne(x => x.Encounter).WithMany(x => x.Participants).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Npc).WithMany().HasForeignKey(x => x.NpcId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CharacterRevision>(entity =>
        {
            entity.ToTable("character_revisions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CharacterId, x.Version }).IsUnique();
            entity.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Character).WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.EditedBy).WithMany().HasForeignKey(x => x.EditedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
