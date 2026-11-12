namespace Chronicles.Api.Models;

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public Guid? SystemVersionId { get; set; }
    public RpgSystemVersion? SystemVersion { get; set; }
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
    public int Version { get; set; } = 1;
}
