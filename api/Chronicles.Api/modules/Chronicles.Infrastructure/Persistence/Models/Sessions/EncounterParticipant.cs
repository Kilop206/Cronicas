namespace Chronicles.Api.Models;

public sealed class EncounterParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public Guid? CharacterId { get; set; }
    public Character? Character { get; set; }
    public Guid? NpcId { get; set; }
    public Npc? Npc { get; set; }
    public int Initiative { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public string Conditions { get; set; } = "";
}
