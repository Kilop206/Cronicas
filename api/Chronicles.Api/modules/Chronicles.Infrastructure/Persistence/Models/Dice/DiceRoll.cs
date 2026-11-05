namespace Chronicles.Api.Models;

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
