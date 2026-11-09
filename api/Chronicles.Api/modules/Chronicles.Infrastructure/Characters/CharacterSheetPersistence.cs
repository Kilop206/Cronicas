using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

internal static class CharacterSheetPersistence
{
    internal static async Task<CharacterSheetSnapshot?> Load(AppDbContext db, Guid id, Guid actor, CancellationToken ct)
    {
        var row = await db.Characters.AsNoTracking().Where(c => c.Id == id && c.Campaign.Memberships.Any(m => m.UserId == actor))
            .Select(c => new { Character = c, c.Campaign.SystemId, Schema = c.Campaign.System == null ? null : c.Campaign.System.SchemaJson,
                Role = c.Campaign.Memberships.Where(m => m.UserId == actor).Select(m => m.Role).First() }).SingleOrDefaultAsync(ct);
        return row is null ? null : Snapshot(row.Character, row.Role, row.Schema is null ? null : SystemDefinition.Read(row.Schema), row.SystemId);
    }

    internal static async Task LockContext(AppDbContext db, Guid campaignId, Guid actor, Guid? systemId, CancellationToken ct)
    {
        await db.CampaignMemberships.FromSqlInterpolated($"SELECT * FROM campaign_memberships WHERE \"CampaignId\" = {campaignId} AND \"UserId\" = {actor} FOR SHARE").ToListAsync(ct);
        await db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {campaignId} FOR SHARE").ToListAsync(ct);
        if (systemId is { } system)
            await db.RpgSystems.FromSqlInterpolated($"SELECT * FROM rpg_systems WHERE \"Id\" = {system} FOR SHARE").ToListAsync(ct);
    }

    internal static bool SameSystem(Guid? firstId, SystemDefinition? first, Guid? secondId, SystemDefinition? second) =>
        firstId == secondId && JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);

    internal static CharacterSheetSnapshot Snapshot(Character c, string role, SystemDefinition? system, Guid? systemId) =>
        new(c.Id, c.CampaignId, c.OwnerId, role, State(c), system, c.UpdatedAt, systemId);

    internal static CharacterSheetState State(Character c) => new(c.Name, c.PortraitUrl, c.BackgroundUrl, c.Health, c.MaxHealth,
        c.Sanity, c.MaxSanity, c.Mana, c.MaxMana, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson), c.Version);

    internal static void Apply(Character c, CharacterSheetState s)
    {
        c.Name = s.Name; c.PortraitUrl = s.PortraitUrl; c.BackgroundUrl = s.BackgroundUrl;
        c.Health = s.Health; c.MaxHealth = s.MaxHealth; c.Sanity = s.Sanity; c.MaxSanity = s.MaxSanity;
        c.Mana = s.Mana; c.MaxMana = s.MaxMana; c.FieldsJson = JsonSerializer.Serialize(s.Fields ?? []); c.UpdatedAt = DateTime.UtcNow;
    }

    internal static bool IsConflict(Exception error)
    {
        if (error is DbUpdateConcurrencyException) return true;
        var postgres = error as Npgsql.PostgresException ?? (error as DbUpdateException)?.InnerException as Npgsql.PostgresException;
        // EF may insert the history row before updating the character's concurrency token.
        // Only this known revision collision is a concurrency failure; unrelated unique violations remain errors.
        return postgres?.SqlState is "40001" or "40P01" || postgres is
            { SqlState: "23505", ConstraintName: "IX_character_revisions_CharacterId_Version" };
    }
}
