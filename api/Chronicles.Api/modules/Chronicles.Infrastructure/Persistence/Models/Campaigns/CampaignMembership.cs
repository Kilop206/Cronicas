namespace Chronicles.Api.Models;

public sealed class CampaignMembership
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "player";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
