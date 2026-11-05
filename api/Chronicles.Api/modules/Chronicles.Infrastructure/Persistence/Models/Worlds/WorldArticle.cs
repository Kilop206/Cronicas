namespace Chronicles.Api.Models;

public sealed class WorldArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorldId { get; set; }
    public World World { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Title { get; set; }
    public string Body { get; set; } = "";
    public string Category { get; set; } = "general";
    public string Visibility { get; set; } = "private";
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
