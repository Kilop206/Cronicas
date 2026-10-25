namespace Chronicles.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string Accent { get; set; } = "gold";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CampaignMembership> Memberships { get; set; } = [];
}

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class PasswordResetCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string CodeHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public sealed class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public string Status { get; set; } = "planning";
    public string AccentColor { get; set; } = "#b39a69";
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CampaignMembership> Memberships { get; set; } = [];
    public Guid? RealtimeMeetingId { get; set; }
    public Guid? SystemId { get; set; }
    public RpgSystem? System { get; set; }
}

public sealed class CampaignMembership
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "player";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public required string Name { get; set; }
    public string? PortraitUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public int Health { get; set; } = 10;
    public int MaxHealth { get; set; } = 10;
    public int Sanity { get; set; } = 10;
    public int MaxSanity { get; set; } = 10;
    public int Mana { get; set; } = 10;
    public int MaxMana { get; set; } = 10;
    public string? OverlayKeyHash { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string FieldsJson { get; set; } = "{}";
}

public sealed class RoomParticipant
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RealtimeParticipantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CampaignInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public required string TokenHash { get; set; }
    public string Role { get; set; } = "player";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? UsedAt { get; set; }
    public Guid? UsedById { get; set; }
}

public sealed class NarrativeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Kind { get; set; }
    public required string Title { get; set; }
    public string Body { get; set; } = "";
    public string Category { get; set; } = "general";
    public string Visibility { get; set; } = "private";
    public DateTime? EventAt { get; set; }
    public DateTime? RevealedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}

public sealed class DiceRoll
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RequestId { get; set; }
    public required string Expression { get; set; }
    public required string RollsJson { get; set; }
    public required string KeptIndexesJson { get; set; }
    public int Modifier { get; set; }
    public int Total { get; set; }
    public string Visibility { get; set; } = "campaign";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class RpgSystem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public string SchemaJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
