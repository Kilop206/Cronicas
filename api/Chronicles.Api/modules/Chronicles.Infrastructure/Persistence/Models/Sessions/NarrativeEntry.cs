namespace Chronicles.Api.Models;

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
