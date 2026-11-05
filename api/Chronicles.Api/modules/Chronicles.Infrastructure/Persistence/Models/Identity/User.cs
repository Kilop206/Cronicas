namespace Chronicles.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string Accent { get; set; } = "gold";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CampaignMembership> Memberships { get; set; } = [];
}
