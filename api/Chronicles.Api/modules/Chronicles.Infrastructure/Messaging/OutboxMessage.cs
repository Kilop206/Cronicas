using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string Payload { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
}

public static class OutboxModel
{
    public static void Configure(ModelBuilder model) => model.Entity<OutboxMessage>(entity =>
    {
        entity.ToTable("integration_outbox");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Payload).HasColumnType("jsonb");
        entity.HasIndex(x => new { x.AvailableAt, x.OccurredAt }).HasFilter("\"ProcessedAt\" IS NULL");
    });
}
