namespace Chronicles.Api.Models;

public sealed class WorldArticleLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public WorldArticle Source { get; set; } = null!;
    public Guid TargetId { get; set; }
    public WorldArticle Target { get; set; } = null!;
    public required string Label { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
