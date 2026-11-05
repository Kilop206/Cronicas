using System.Text.Json;

namespace Chronicles.Domain;

// Transport-neutral game state. HTTP and EF adapt to this contract, never the reverse.
public sealed record CharacterSheetState(string Name, string? PortraitUrl, string? BackgroundUrl,
    int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana,
    Dictionary<string, JsonElement>? Fields = null, int? Version = null);
