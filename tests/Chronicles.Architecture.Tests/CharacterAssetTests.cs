using Chronicles.Application.Characters;
using Chronicles.Domain;

public static class CharacterAssetTests
{
    public static async Task Run()
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Character assets: " + message); }
        var actor = Guid.NewGuid(); var access = new CharacterAssetAccess(Guid.NewGuid(), Guid.NewGuid(), actor, "player");
        var inventory = new MemoryInventory(access); var items = new ManageCharacterInventory(inventory);
        var valid = new InventoryItemValues("  Rope  ", "equipment", 2, 1.25m, "  Notes  ");
        var created = await items.Create(access.CharacterId, actor, valid);
        Check(created.Value is { Name: "Rope", Notes: "Notes", Version: 1 } && inventory.Writes == 1, "inventory validates and normalizes before port");
        Check((await items.Create(access.CharacterId, actor, valid with { Weight = 1.001m })).Failure == CharacterAssetFailure.Invalid && inventory.Writes == 1,
            "invalid precision cannot reach persistence");
        Check((await items.Update(access.CharacterId, created.Value!.Id, actor, 0, valid)).Failure == CharacterAssetFailure.Conflict, "stale item cannot be updated");
        Check((await items.Delete(access.CharacterId, created.Value.Id, actor, 0)).Failure == CharacterAssetFailure.Conflict, "stale item cannot be deleted");
        inventory.Access = access with { Role = "spectator" };
        Check((await items.Delete(access.CharacterId, created.Value.Id, actor, 1)).Failure == CharacterAssetFailure.Forbidden, "spectator cannot delete own inventory");
        inventory.Access = access with { OwnerId = Guid.NewGuid() };
        Check((await items.Create(access.CharacterId, actor, valid)).Failure == CharacterAssetFailure.Forbidden, "player cannot modify another owner");
        inventory.Access = inventory.Access with { Role = "co-gm" };
        Check((await items.Update(access.CharacterId, created.Value.Id, actor, 1, valid)).Value?.Version == 2, "co-GM can edit another owner");
        inventory.Access = null;
        Check((await items.Create(access.CharacterId, actor, valid)).Failure == CharacterAssetFailure.NotFound, "non-member resource stays hidden");

        var abilities = new MemoryAbilities(access); var skills = new ManageCharacterAbilities(abilities);
        var ability = new CharacterAbilityValues("  Action  ", " Description ", " Cost ", " Effect ", " Turn ", " Limit ", " Rest ", " 1D6+2 ");
        var saved = await skills.Create(access.CharacterId, actor, ability);
        Check(saved.Value is { Name: "Action", Description: "Description", RollExpression: "1d6+2" }, "ability normalization preserves all fields");
        Check((await skills.Update(access.CharacterId, saved.Value!.Id, actor, 1, ability with { RollExpression = "eval(alert(1))" })).Failure == CharacterAssetFailure.Invalid && abilities.Writes == 1,
            "unsafe dice never reaches persistence");
        Check((await skills.Delete(access.CharacterId, saved.Value.Id, actor, 0)).Failure == CharacterAssetFailure.Conflict, "ability deletion requires revision");
        Check((await skills.Update(access.CharacterId, Guid.NewGuid(), actor, 1, ability)).Failure == CharacterAssetFailure.NotFound, "unknown ability cannot be mutated");
        abilities.Access = access with { Role = "spectator" };
        Check((await skills.Create(access.CharacterId, actor, ability)).Failure == CharacterAssetFailure.Forbidden, "spectator cannot add abilities");
        Check(new InventoryItemValues(null!, "invalid", 0, -1, null!).Validate()!.Count == 5, "inventory nulls/ranges/categories validated without throwing");
        Check(ability with { Description = null! } is { } missing && missing.Validate()!.ContainsKey("ability"), "ability null fields validated without throwing");
        Console.WriteLine("Character assets application: normalization, precise weights, safe dice, owner/master/spectator permissions, revisioned edits/deletes and hidden IDs passed.");
    }

    private abstract class MemoryAccess(CharacterAssetAccess access) : ICharacterAssetAccessReader
    {
        public CharacterAssetAccess? Access = access;
        public int Writes;
        public Task<CharacterAssetAccess?> LoadAccess(Guid character, Guid actor, CancellationToken ct) => Task.FromResult(Access);
    }
    private sealed class MemoryInventory(CharacterAssetAccess access) : MemoryAccess(access), ICharacterInventoryStore
    {
        private InventoryItemDocument? item;
        public Task<InventoryItemDocument[]?> List(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<InventoryItemDocument[]?>(item is null ? [] : [item]);
        public Task<InventoryItemDocument?> Find(Guid character, Guid id, Guid actor, CancellationToken ct) => Task.FromResult(item?.Id == id ? item : null);
        public Task<CharacterAssetResult<InventoryItemDocument>> Create(CharacterAssetAccess a, InventoryItemValues v, Guid actor, CancellationToken ct)
        { Writes++; item = new(Guid.NewGuid(), a.CharacterId, v.Name, v.Category, v.Quantity, v.Weight, v.Notes, 1, DateTime.UtcNow, DateTime.UtcNow); return Task.FromResult(new CharacterAssetResult<InventoryItemDocument>(item)); }
        public Task<CharacterAssetResult<InventoryItemDocument>> Update(CharacterAssetAccess a, InventoryItemDocument before, InventoryItemValues v, Guid actor, CancellationToken ct)
        { Writes++; item = before with { Version = before.Version + 1 }; return Task.FromResult(new CharacterAssetResult<InventoryItemDocument>(item)); }
        public Task<CharacterAssetResult<InventoryItemDocument>> Delete(CharacterAssetAccess a, InventoryItemDocument before, Guid actor, CancellationToken ct)
        { Writes++; item = null; return Task.FromResult(new CharacterAssetResult<InventoryItemDocument>()); }
    }
    private sealed class MemoryAbilities(CharacterAssetAccess access) : MemoryAccess(access), ICharacterAbilityStore
    {
        private CharacterAbilityDocument? ability;
        public Task<CharacterAbilityDocument[]?> List(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<CharacterAbilityDocument[]?>(ability is null ? [] : [ability]);
        public Task<CharacterAbilityDocument?> Find(Guid character, Guid id, Guid actor, CancellationToken ct) => Task.FromResult(ability?.Id == id ? ability : null);
        public Task<CharacterAssetResult<CharacterAbilityDocument>> Create(CharacterAssetAccess a, CharacterAbilityValues v, Guid actor, CancellationToken ct)
        { Writes++; ability = new(Guid.NewGuid(), a.CharacterId, v.Name, v.Description, v.Cost, v.Effect, v.Duration, v.Limitations, v.Recharge, v.RollExpression, 1, DateTime.UtcNow, DateTime.UtcNow); return Task.FromResult(new CharacterAssetResult<CharacterAbilityDocument>(ability)); }
        public Task<CharacterAssetResult<CharacterAbilityDocument>> Update(CharacterAssetAccess a, CharacterAbilityDocument before, CharacterAbilityValues v, Guid actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<CharacterAssetResult<CharacterAbilityDocument>> Delete(CharacterAssetAccess a, CharacterAbilityDocument before, Guid actor, CancellationToken ct) => throw new NotSupportedException();
    }
}
