namespace Chronicles.Api.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ClientId { get; set; }
    public required string Body { get; set; }
    public string Kind { get; set; } = "chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
