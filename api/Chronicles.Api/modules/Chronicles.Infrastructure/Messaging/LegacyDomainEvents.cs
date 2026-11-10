using System.Diagnostics;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Messaging;

// Anti-corruption bridge while legacy write paths migrate to application use cases.
// Events become durable in the same SaveChanges transaction as the business write.
internal static class LegacyDomainEvents
{
    internal static void Stage(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var events = new List<GameEvent>();
        foreach (var entry in db.ChangeTracker.Entries().ToArray())
        {
            var added = entry.State == EntityState.Added;
            var changed = entry.State == EntityState.Modified;
            var deleted = entry.State == EntityState.Deleted;
            void Add(string type, Guid id, Guid? campaign = null, int? revision = null) => events.Add(new(Guid.NewGuid(), type, 1,
                DateTime.UtcNow, id, campaign, revision, Activity.Current?.GetTagItem("chronicles.correlation_id")?.ToString() ?? Activity.Current?.TraceId.ToString()));
            switch (entry.Entity)
            {
                case Character c when added || changed || deleted:
                    Add(added ? GameEventTypes.CharacterCreated : deleted ? GameEventTypes.CharacterDeleted : GameEventTypes.CharacterUpdated,
                        c.Id, c.CampaignId, c.Version);
                    if (changed) Add(GameEventTypes.CharacterSheetUpdated, c.Id, c.CampaignId, c.Version);
                    break;
                case Campaign c when added: Add(GameEventTypes.CampaignCreated, c.Id, c.Id); break;
                case CampaignMembership m when added: Add(GameEventTypes.PlayerJoinedCampaign, m.UserId, m.CampaignId); break;
                case CampaignMembership m when changed && entry.Property(nameof(m.Role)).IsModified:
                    Add(GameEventTypes.CampaignMemberRoleChanged, m.UserId, m.CampaignId); break;
                case CampaignMembership m when deleted: Add(GameEventTypes.CampaignMemberLeft, m.UserId, m.CampaignId); break;
                case DiceRoll d when added: Add(GameEventTypes.DiceRolled, d.Id, d.CampaignId); break;
                case RpgSystem s when added: Add(GameEventTypes.RPGSystemRegistered, s.Id); break;
            }
        }
        foreach (var message in events) db.OutboxMessages.Add(new() { Id = message.EventId, OccurredAt = message.OccurredAt,
            AvailableAt = message.OccurredAt, Payload = JsonSerializer.Serialize(message) });
    }
}
