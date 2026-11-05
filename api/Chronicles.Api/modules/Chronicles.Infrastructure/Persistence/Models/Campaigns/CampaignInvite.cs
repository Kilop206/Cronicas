namespace Chronicles.Api.Models;

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
