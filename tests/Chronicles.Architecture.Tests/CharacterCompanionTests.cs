using System.Text.Json;
using Chronicles.Application.Characters;
using Chronicles.Domain;

public static class CharacterCompanionTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Companion application: " + label); }
        var actor = Guid.NewGuid(); var access = new CharacterAssetAccess(Guid.NewGuid(), Guid.NewGuid(), actor, "player");
        var store = new MemoryStore(access); var favorites = new ManageCharacterFavorites(store);
        var secrets = new ManageCharacterSecrets(store); var keys = new FixedKey();
        var overlay = new ManageCharacterOverlay(store, keys, RpgPluginRegistry.Default);
        Check((await favorites.Set(access.CharacterId, actor, true)).Value!.Favorite, "member can favorite own or another character");
        Check(await secrets.Read(access.CharacterId, actor) is null && (await secrets.Save(access.CharacterId, actor, new("hidden", 1))).Failure == CharacterAssetFailure.NotFound,
            "owning the sheet does not reveal master notes");
        Check((await overlay.Rotate(access.CharacterId, actor)).Value?.Key == keys.Key && store.Hash == OverlayCapability.Hash(keys.Key), "only hashed capability crosses persistence port");
        Check((await overlay.Read(keys.Key.ToUpperInvariant())) is not null, "capabilities normalize hex case");
        var reads = store.OverlayReads;
        Check(await overlay.Read("not-a-key") is null && store.OverlayReads == reads, "malformed key does not query persistence");
        store.Access = access with { Role = "spectator" };
        Check((await favorites.Set(access.CharacterId, actor, true)).Failure is null && (await overlay.Rotate(access.CharacterId, actor)).Failure == CharacterAssetFailure.NotFound,
            "spectator can favorite but cannot rotate OBS");
        store.Access = access with { Role = "gm", OwnerId = Guid.NewGuid() };
        Check((await overlay.Rotate(access.CharacterId, actor)).Failure == CharacterAssetFailure.NotFound, "master cannot rotate another owner's OBS");
        Check((await secrets.Save(access.CharacterId, actor, new(null!, 1))).Failure == CharacterAssetFailure.Invalid, "null notes validated");
        var saved = await secrets.Save(access.CharacterId, actor, new("  Plans  ", 1));
        Check(saved.Value?.Notes == "Plans" && saved.Value.Revision == 1, "first legacy secret keeps revision one and normalizes notes");
        Check((await secrets.Save(access.CharacterId, actor, new("Stale empty draft", 1, false))).Failure == CharacterAssetFailure.Conflict,
            "explicit absence blocks overwrite even when initial legacy revision still matches");
        store.Secret = new("Newer", 3, DateTime.UtcNow);
        Check((await secrets.Save(access.CharacterId, actor, new("Stale", 1))).Failure == CharacterAssetFailure.Conflict, "stale secret never reaches commit");
        store.Access = null;
        Check((await favorites.Set(access.CharacterId, actor, true)).Failure == CharacterAssetFailure.NotFound, "non-member favorite hidden");

        var state = new CharacterSheetState("Agent", null, null, 12, 30, 20, 40, 5, 10, new()
        { ["nex"] = JsonSerializer.SerializeToElement(35), ["defesa"] = JsonSerializer.SerializeToElement(18), ["nivel"] = JsonSerializer.SerializeToElement(5),
          ["sorte"] = JsonSerializer.SerializeToElement(50), ["secret_text"] = JsonSerializer.SerializeToElement("must not leak") }, 2);
        foreach (var (id, version, edition, labels) in new[] {
            ("ordem_paranormal", 7, "1.3", "PV,PE,SAN"), ("dnd_5e", 5, "2014", "HP"),
            ("tormenta20", 4, "jogo-do-ano-2024-v1.3", "PV,PM"), ("call_of_cthulhu_7e", 6, "7e", "HP,SAN,Magic Points"), ("dnd_5e", 1, "2024", "HP") })
        {
            var result = CharacterOverlayProjection.Document(new(state, new([], null, id, version, edition), DateTime.UtcNow), RpgPluginRegistry.Default);
            Check(string.Join(',', result.Resources.Select(r => r.Label)) == labels, "edition profile " + id + edition);
            Check(!JsonSerializer.Serialize(result).Contains("must not leak"), "projection whitelists fields");
        }
        var layout = new SheetLayout("#a54d52", [new("main", "Custom", [new("focus", "tracker", 6, "Focus", "focus", MaxKey: "limit"),
            new("score", "field", 6, "Luck", "sorte", Style: "score")])]);
        var customState = state with { Fields = new(state.Fields!) { ["focus"] = JsonSerializer.SerializeToElement(3), ["limit"] = JsonSerializer.SerializeToElement(8) } };
        var unsupported = new SystemDefinition([], layout, "ordem_paranormal", 7, "future");
        var custom = CharacterOverlayProjection.Document(new(customState, unsupported, DateTime.UtcNow), RpgPluginRegistry.Default);
        Check(custom.Resources is [{ Label: "Focus", Current: 3, Max: 8 }] && custom.Indicators is [{ Label: "Luck", Value: "50" }], "unknown edition uses configured layout, not another edition's profile");
        var ruleField = new SystemField("estado_regras", "State", "table", "Rules", null, null, null);
        var conditioned = state with { Fields = new(state.Fields!) { ["estado_regras"] = JsonSerializer.SerializeToElement(new[] { new { chave = "cond_vulneravel", valor = 1 } }) } };
        var defended = CharacterOverlayProjection.Document(new(conditioned, new([ruleField], null, "ordem_paranormal", 7, "1.3"), DateTime.UtcNow), RpgPluginRegistry.Default);
        Check(defended.Indicators.Single(i => i.Label == "Defesa").Value == "16", "OBS derives defense from the configured edition's conditions");
        Check(conditioned.Fields!["defesa"].GetInt32() == 18, "projection does not change persisted state");
        Console.WriteLine("Companion application: role policies, private notes, hash-only capability, four RPG profiles, custom/unknown editions and condition-aware safe projection passed.");
    }

    private sealed class FixedKey : IOverlayKeyGenerator { public string Key = new('a', 64); public string Generate() => Key; }
    private sealed class MemoryStore(CharacterAssetAccess access) : ICharacterFavoriteStore, ICharacterSecretStore, ICharacterOverlayStore
    {
        public CharacterAssetAccess? Access = access; public CharacterSecretDocument Secret = new("", 1, null); public string? Hash; public int OverlayReads;
        public Task<CharacterAssetAccess?> LoadAccess(Guid character, Guid actor, CancellationToken ct) => Task.FromResult(Access);
        public Task<CharacterFavoriteDocument?> ReadFavorite(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<CharacterFavoriteDocument?>(new(false));
        public Task<CharacterAssetResult<CharacterFavoriteDocument>> SetFavorite(CharacterAssetAccess a, Guid actor, bool value, CancellationToken ct) => Task.FromResult(new CharacterAssetResult<CharacterFavoriteDocument>(new(value)));
        public Task<CharacterSecretDocument?> ReadSecret(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<CharacterSecretDocument?>(Secret);
        public Task<CharacterAssetResult<CharacterSecretDocument>> SaveSecret(CharacterAssetAccess a, CharacterSecretDocument before, CharacterSecretValues value, Guid actor, CancellationToken ct)
        { Secret = new(value.Notes, value.Revision, DateTime.UtcNow); return Task.FromResult(new CharacterAssetResult<CharacterSecretDocument>(Secret)); }
        public Task<CharacterAssetResult<bool>> RotateOverlay(CharacterAssetAccess a, string hash, Guid actor, CancellationToken ct)
        { Hash = hash; return Task.FromResult(new CharacterAssetResult<bool>(true)); }
        public Task<CharacterOverlaySource?> ReadOverlay(string hash, CancellationToken ct)
        { OverlayReads++; return Task.FromResult<CharacterOverlaySource?>(hash == Hash ? new(new("Public", null, null, 1, 2, 1, 2, 1, 2), null, DateTime.UtcNow) : null); }
    }
}
