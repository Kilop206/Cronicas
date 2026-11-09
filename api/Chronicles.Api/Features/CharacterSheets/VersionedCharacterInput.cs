using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronicles.Api;

// HTTP v1 requires a revision; the transport-neutral state remains compatible with legacy writers.
public sealed record VersionedCharacterInput(string Name, string? PortraitUrl, string? BackgroundUrl,
    int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana,
    [property: JsonRequired] int Version, Dictionary<string, JsonElement>? Fields = null)
{
    public CharacterSheetState ToState() => new(Name, PortraitUrl, BackgroundUrl, Health, MaxHealth,
        Sanity, MaxSanity, Mana, MaxMana, Fields, Version);
}
