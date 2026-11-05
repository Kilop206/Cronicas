namespace Chronicles.Api.Models;

public sealed class Npc
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Name { get; set; }
    public string? PortraitUrl { get; set; }
    public string Appearance { get; set; } = "";
    public string Personality { get; set; } = "";
    public string Motivation { get; set; } = "";
    public string Relationships { get; set; } = "";
    public string Location { get; set; } = "";
    public string Secrets { get; set; } = "";
    public string Status { get; set; } = "active";
    public string Visibility { get; set; } = "gm";
    public int Health { get; set; } = 10;
    public int MaxHealth { get; set; } = 10;
    public string? SheetJson { get; set; }
    public string HistoryJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
