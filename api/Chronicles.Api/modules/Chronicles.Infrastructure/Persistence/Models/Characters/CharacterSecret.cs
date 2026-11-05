namespace Chronicles.Api.Models;

public sealed class CharacterSecret
{
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public string Notes { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
