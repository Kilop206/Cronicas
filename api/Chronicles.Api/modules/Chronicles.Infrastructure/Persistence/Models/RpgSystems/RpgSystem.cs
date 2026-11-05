namespace Chronicles.Api.Models;

public sealed class RpgSystem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public string SchemaJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
