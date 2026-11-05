namespace Chronicles.Api.Models;

public sealed class CharacterAbility
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public string Cost { get; set; } = "";
    public string Effect { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Limitations { get; set; } = "";
    public string Recharge { get; set; } = "";
    public string? RollExpression { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
