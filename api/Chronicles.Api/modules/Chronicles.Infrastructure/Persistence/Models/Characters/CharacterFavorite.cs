namespace Chronicles.Api.Models;

public sealed class CharacterFavorite
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
