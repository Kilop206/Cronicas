namespace Chronicles.Api.Models;

public sealed class CharacterInventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public required string Name { get; set; }
    public string Category { get; set; } = "item";
    public int Quantity { get; set; } = 1;
    public decimal Weight { get; set; }
    public string Notes { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
