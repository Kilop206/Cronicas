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
    public async Task<CharacterRuleSnapshot?> Load(Guid id, Guid actor, CancellationToken ct)
    {
        var row = await db.Characters.AsNoTracking().Where(c => c.Id == id && c.Campaign.Memberships.Any(m => m.UserId == actor))
            .Select(c => new { Character = c, c.Campaign.SystemId, Schema = c.Campaign.System == null ? null : c.Campaign.System.SchemaJson,
                Role = c.Campaign.Memberships.Where(m => m.UserId == actor).Select(m => m.Role).First() }).SingleOrDefaultAsync(ct);
        return row is null ? null : Snapshot(row.Character, row.Role, row.Schema is null ? null : SystemDefinition.Read(row.Schema), row.SystemId);
    }

    public async Task<CharacterRuleSnapshot?> Commit(CharacterRuleChange change, CancellationToken ct)
    {
        // Recheck access and model revision inside the transaction. Lock membership, campaign and system
        // against concurrent revocation/template migration, without turning sheet edits into last-write-wins.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try
        {
            var before = change.Before;
            await db.CampaignMemberships.FromSqlInterpolated($"SELECT * FROM campaign_memberships WHERE \"CampaignId\" = {before.CampaignId} AND \"UserId\" = {change.ActorId} FOR SHARE").ToListAsync(ct);
            await db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {before.CampaignId} FOR SHARE").ToListAsync(ct);
            if (before.SystemId is { } systemId)
                await db.RpgSystems.FromSqlInterpolated($"SELECT * FROM rpg_systems WHERE \"Id\" = {systemId} FOR SHARE").ToListAsync(ct);
            var fresh = await Load(before.Id, change.ActorId, ct);
            if (fresh is null || !CharacterAccessPolicy.CanEdit(fresh.OwnerId, change.ActorId, fresh.Role) || fresh.Role != before.Role ||
                fresh.State.Version != before.State.Version || fresh.SystemId != before.SystemId ||
                JsonSerializer.Serialize(fresh.System) != JsonSerializer.Serialize(before.System)) return null;
            var character = await db.Characters.SingleAsync(c => c.Id == before.Id, ct);
            var state = change.Result.Character;
            var changed = !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(fresh.State with { Version = null }),
                JsonSerializer.SerializeToElement(state with { Version = null }));
            if (changed)
            {
                CharacterHistory.Capture(db, character, change.ActorId);
                character.Name = state.Name.Trim(); character.PortraitUrl = state.PortraitUrl?.Trim(); character.BackgroundUrl = state.BackgroundUrl?.Trim();
                character.Health = state.Health; character.MaxHealth = state.MaxHealth;
                character.Sanity = state.Sanity; character.MaxSanity = state.MaxSanity;
                character.Mana = state.Mana; character.MaxMana = state.MaxMana;
                character.FieldsJson = JsonSerializer.Serialize(state.Fields ?? []); character.UpdatedAt = DateTime.UtcNow;
                await CharacterEncounterConditions.Sync(db, character, fresh.System!);
            }
            foreach (var roll in change.Result.Rolls)
                db.DiceRolls.Add(new DiceRoll { CampaignId = character.CampaignId, UserId = change.ActorId, RequestId = Guid.NewGuid(),
                    Expression = roll.Expression, RollsJson = JsonSerializer.Serialize(roll.Rolls), KeptIndexesJson = JsonSerializer.Serialize(roll.KeptIndexes),
                    Modifier = roll.Modifier, Total = roll.Total, Visibility = change.Visibility });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Snapshot(character, fresh.Role, fresh.System, fresh.SystemId);
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return null; }
        catch (Npgsql.PostgresException error) when (error.SqlState == "40P01") { db.ChangeTracker.Clear(); return null; }
        catch (Npgsql.PostgresException error) when (error.SqlState == "40001") { db.ChangeTracker.Clear(); return null; }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "40001" }) { db.ChangeTracker.Clear(); return null; }
    }

    private static CharacterRuleSnapshot Snapshot(Character c, string role, SystemDefinition? system, Guid? systemId) =>
        new(c.Id, c.CampaignId, c.OwnerId, role, new(c.Name, c.PortraitUrl, c.BackgroundUrl, c.Health, c.MaxHealth, c.Sanity, c.MaxSanity,
            c.Mana, c.MaxMana, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson), c.Version), system, c.UpdatedAt, systemId);
}
