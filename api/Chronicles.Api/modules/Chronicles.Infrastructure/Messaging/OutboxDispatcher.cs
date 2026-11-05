using System.Diagnostics.Metrics;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Application.Events;
using Chronicles.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Messaging;

public sealed class OutboxDispatcher(AppDbContext db, IEnumerable<IGameEventHandler> handlers)
{
    private static readonly Meter Meter = new("Chronicles.Outbox", "1.0.0");
    private static readonly Counter<long> Delivered = Meter.CreateCounter<long>("chronicles.outbox.delivered");
    private static readonly Counter<long> Retried = Meter.CreateCounter<long>("chronicles.outbox.retried");

    public async Task<int> Dispatch(int limit = 20, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var processed = 0;
        for (var i = 0; i < limit && !ct.IsCancellationRequested; i++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var row = (await db.OutboxMessages.FromSqlRaw("""
                SELECT * FROM integration_outbox
                WHERE "ProcessedAt" IS NULL AND "AvailableAt" <= now()
                ORDER BY "OccurredAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).AsTracking().ToListAsync(ct)).SingleOrDefault();
            if (row is null) break;
            row.Attempts++;
            try
            {
                var message = JsonSerializer.Deserialize<GameEvent>(row.Payload) ?? throw new InvalidOperationException("Invalid outbox event.");
                if (message.ContractVersion != 1) throw new InvalidOperationException("Unsupported event version.");
                foreach (var handler in handlers) await handler.Handle(message, ct);
                row.ProcessedAt = DateTime.UtcNow;
                processed++;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Poison messages remain visible and retriable; no payload/PII in error strings.
                row.AvailableAt = DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(row.Attempts, 8))));
                Retried.Add(1);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            if (row.ProcessedAt is not null) Delivered.Add(1);
            db.ChangeTracker.Clear();
        }
        return processed;
    }
}
