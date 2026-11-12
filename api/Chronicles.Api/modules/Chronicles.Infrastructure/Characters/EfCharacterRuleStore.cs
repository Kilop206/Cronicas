using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class CryptographicGameRandom : IGameRandom
{
    public int Draw(int sides) => sides is >= 2 and <= 10000 ? RandomNumberGenerator.GetInt32(1, sides + 1)
        : throw new ArgumentException("Invalid dice sides.");
}

public sealed class EfCharacterRuleStore(AppDbContext db) : ICharacterRuleStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid id, Guid actor, CancellationToken ct) => CharacterSheetPersistence.Load(db, id, actor, ct);

    public async Task<CharacterSheetSnapshot?> Commit(CharacterRuleChange change, CancellationToken ct)
    {
        // Recheck access and model revision inside the transaction. Lock membership, campaign and system
        // against concurrent revocation/template migration, without turning sheet edits into last-write-wins.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try
        {
            var before = change.Before;
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, change.ActorId, before.SystemId, ct);
            var fresh = await Load(before.Id, change.ActorId, ct);
            if (fresh is null || !CharacterAccessPolicy.CanEdit(fresh.OwnerId, change.ActorId, fresh.Role) || fresh.Role != before.Role ||
                fresh.CampaignId != before.CampaignId || fresh.State.Version != before.State.Version || fresh.SystemId != before.SystemId ||
                JsonSerializer.Serialize(fresh.System) != JsonSerializer.Serialize(before.System)) return null;
            var character = await db.Characters.SingleAsync(c => c.Id == before.Id, ct);
            var state = change.Result.Character;
            var changed = !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(fresh.State with { Version = null }),
                JsonSerializer.SerializeToElement(state with { Version = null }));
            if (changed)
            {
                CharacterHistory.Capture(db, character, change.ActorId);
                CharacterSheetPersistence.Apply(character, state);
                await CharacterEncounterConditions.Sync(db, character, fresh.System!);
            }
            foreach (var roll in change.Result.Rolls)
                db.DiceRolls.Add(new DiceRoll { CampaignId = character.CampaignId, UserId = change.ActorId, RequestId = Guid.NewGuid(),
                    Expression = roll.Expression, RollsJson = JsonSerializer.Serialize(roll.Rolls), KeptIndexesJson = JsonSerializer.Serialize(roll.KeptIndexes),
                    Modifier = roll.Modifier, Total = roll.Total, Visibility = change.Visibility });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return CharacterSheetPersistence.Snapshot(character, fresh.Role, fresh.System, fresh.SystemId);
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error)) { db.ChangeTracker.Clear(); return null; }
    }
}
