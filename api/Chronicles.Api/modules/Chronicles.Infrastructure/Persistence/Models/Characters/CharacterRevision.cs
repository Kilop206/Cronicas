namespace Chronicles.Api.Models;

public sealed class CharacterRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public Guid EditedById { get; set; }
    public User EditedBy { get; set; } = null!;
    public int Version { get; set; }
    public required string SnapshotJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
