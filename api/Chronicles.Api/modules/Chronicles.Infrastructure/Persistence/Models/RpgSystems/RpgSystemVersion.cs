namespace Chronicles.Api.Models;

public sealed class RpgSystemVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RpgSystemId { get; set; }
    public RpgSystem System { get; set; } = null!;
    public required string Name { get; set; }
    public string Description { get; set; } = "";
    public required string DefinitionJson { get; set; }
    public required string ContentHash { get; set; }
    public int SourceRevision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
