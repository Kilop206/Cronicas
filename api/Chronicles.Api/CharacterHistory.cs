using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;

namespace Chronicles.Api;

public static class CharacterHistory
{
    public static void Capture(AppDbContext db, Character character, Guid editedById)
    {
        var snapshot = new CharacterSnapshot(character.Name, character.PortraitUrl, character.BackgroundUrl,
            character.Health, character.MaxHealth, character.Sanity, character.MaxSanity, character.Mana, character.MaxMana, character.FieldsJson);
        db.CharacterRevisions.Add(new CharacterRevision { CharacterId = character.Id, EditedById = editedById,
            Version = character.Version, SnapshotJson = JsonSerializer.Serialize(snapshot) });
        character.Version++;
    }

    public static void Restore(Character character, CharacterSnapshot snapshot)
    {
        character.Name = snapshot.Name; character.PortraitUrl = snapshot.PortraitUrl; character.BackgroundUrl = snapshot.BackgroundUrl;
        character.Health = snapshot.Health; character.MaxHealth = snapshot.MaxHealth;
        character.Sanity = snapshot.Sanity; character.MaxSanity = snapshot.MaxSanity;
        character.Mana = snapshot.Mana; character.MaxMana = snapshot.MaxMana;
        character.FieldsJson = snapshot.FieldsJson;
        character.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record CharacterSnapshot(string Name, string? PortraitUrl, string? BackgroundUrl,
    int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana, string FieldsJson);
