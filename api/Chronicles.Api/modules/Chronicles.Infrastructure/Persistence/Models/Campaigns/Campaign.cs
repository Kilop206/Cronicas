namespace Chronicles.Api.Models;

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
    public Guid? SystemId { get; set; }
    public RpgSystem? System { get; set; }
    public Guid? WorldId { get; set; }
    public World? World { get; set; }
}
