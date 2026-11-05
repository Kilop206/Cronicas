namespace Chronicles.Api.Models;

public sealed class Encounter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public required string Title { get; set; }
    public string Status { get; set; } = "planning";
    public int Round { get; set; } = 1;
    public int TurnIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<EncounterParticipant> Participants { get; set; } = [];
}
