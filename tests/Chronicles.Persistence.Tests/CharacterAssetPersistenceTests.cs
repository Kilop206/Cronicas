using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class CharacterAssetPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Character assets PostgreSQL: " + message); }
        var owner = new User { Email = "assets-owner@example.test", DisplayName = "Owner", PasswordHash = "fixture" };
        var player = new User { Email = "assets-player@example.test", DisplayName = "Player", PasswordHash = "fixture" };
        var spectator = new User { Email = "assets-spectator@example.test", DisplayName = "Spectator", PasswordHash = "fixture" };
        var campaign = new Campaign { Name = "Assets fixture", OwnerId = owner.Id };
        var character = new Character { Name = "Editable", CampaignId = campaign.Id, OwnerId = player.Id };
        var other = new Character { Name = "Other", CampaignId = campaign.Id, OwnerId = owner.Id };
        var full = new Character { Name = "Capacity", CampaignId = campaign.Id, OwnerId = player.Id };
        await using (var db = context())
        {
            db.Users.AddRange(owner, player, spectator); db.Campaigns.Add(campaign); db.Characters.AddRange(character, other, full);
            foreach (var (id, role) in new[] { (owner.Id, "owner"), (player.Id, "player"), (spectator.Id, "spectator") })
                db.CampaignMemberships.Add(new() { CampaignId = campaign.Id, UserId = id, Role = role });
            for (var i = 0; i < InventoryItemValues.Capacity - 1; i++) db.CharacterInventoryItems.Add(new() { CharacterId = full.Id, Name = "Item " + i });
            for (var i = 0; i < CharacterAbilityValues.Capacity - 1; i++) db.CharacterAbilities.Add(new() { CharacterId = full.Id, Name = "Ability " + i });
            await db.SaveChangesAsync();
        }
        var itemValues = new InventoryItemValues(" Rope ", "equipment", 2, 1.25m, " Keep ");
        var abilityValues = new CharacterAbilityValues(" Action ", " Description ", " 2 ", " Effect ", " Scene ", " Limit ", " Rest ", " 1D6+2 ");
        InventoryItemDocument item; CharacterAbilityDocument ability;
        await using (var db = context())
        {
            var inventory = new ManageCharacterInventory(new EfCharacterInventoryStore(db));
            item = (await inventory.Create(character.Id, player.Id, itemValues)).Value!;
            var abilities = new ManageCharacterAbilities(new EfCharacterAbilityStore(db));
            ability = (await abilities.Create(character.Id, player.Id, abilityValues)).Value!;
            Check(item is { Name: "Rope", Notes: "Keep", Version: 1 } && ability is { Name: "Action", RollExpression: "1d6+2", Version: 1 }, "normalized state persisted");
            Check((await inventory.Update(other.Id, item!.Id, owner.Id, 1, itemValues)).Failure == CharacterAssetFailure.NotFound &&
                (await abilities.Delete(other.Id, ability!.Id, owner.Id, 1)).Failure == CharacterAssetFailure.NotFound, "child IDs scoped to their parent");
            Check(await inventory.List(character.Id, Guid.NewGuid()) is null && await abilities.List(character.Id, Guid.NewGuid()) is null, "non-member cannot list children");
            Check((await inventory.List(character.Id, spectator.Id))!.Length == 1 && (await abilities.List(character.Id, spectator.Id))!.Length == 1, "spectator can read public collections");
            Check((await inventory.Delete(character.Id, item.Id, spectator.Id, 1)).Failure == CharacterAssetFailure.Forbidden &&
                (await abilities.Update(character.Id, ability.Id, spectator.Id, 1, abilityValues)).Failure == CharacterAssetFailure.Forbidden, "spectator cannot mutate children");
        }

        async Task<CharacterAssetResult<InventoryItemDocument>> SaveItem(int quantity)
        {
            await using var db = context();
            return await new ManageCharacterInventory(new EfCharacterInventoryStore(db)).Update(character.Id, item.Id, player.Id, 1, itemValues with { Quantity = quantity });
        }
        var updates = await Task.WhenAll(SaveItem(3), SaveItem(4));
        Check(updates.Count(r => r.Failure is null) == 1 && updates.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1, "concurrent item update has one winner");
        async Task<CharacterAssetResult<CharacterAbilityDocument>> SaveAbility(string effect)
        {
            await using var db = context();
            return await new ManageCharacterAbilities(new EfCharacterAbilityStore(db)).Update(character.Id, ability.Id, player.Id, 1, abilityValues with { Effect = effect });
        }
        var abilityUpdates = await Task.WhenAll(SaveAbility("First"), SaveAbility("Second"));
        Check(abilityUpdates.Count(r => r.Failure is null) == 1 && abilityUpdates.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1, "concurrent ability update has one winner");

        async Task<CharacterAssetResult<InventoryItemDocument>> AddItem()
        {
            await using var db = context(); return await new ManageCharacterInventory(new EfCharacterInventoryStore(db)).Create(full.Id, player.Id, itemValues);
        }
        async Task<CharacterAssetResult<CharacterAbilityDocument>> AddAbility()
        {
            await using var db = context(); return await new ManageCharacterAbilities(new EfCharacterAbilityStore(db)).Create(full.Id, player.Id, abilityValues);
        }
        var additions = await Task.WhenAll(AddItem(), AddItem());
        var abilityAdditions = await Task.WhenAll(AddAbility(), AddAbility());
        Check(additions.Count(r => r.Failure is null) == 1 && additions.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1, "concurrent inventory creation respects capacity");
        Check(abilityAdditions.Count(r => r.Failure is null) == 1 && abilityAdditions.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1, "concurrent ability creation respects capacity");
        await using (var db = context())
        {
            Check(await db.CharacterInventoryItems.CountAsync(i => i.CharacterId == full.Id) == 200 &&
                await db.CharacterAbilities.CountAsync(a => a.CharacterId == full.Id) == 100, "persisted capacities never exceeded");
            var items = new ManageCharacterInventory(new EfCharacterInventoryStore(db)); var skills = new ManageCharacterAbilities(new EfCharacterAbilityStore(db));
            Check((await items.Delete(character.Id, item.Id, owner.Id, 1)).Failure == CharacterAssetFailure.Conflict &&
                (await skills.Delete(character.Id, ability.Id, owner.Id, 1)).Failure == CharacterAssetFailure.Conflict, "stale deletes preserve the latest child");
            Check((await items.List(character.Id, player.Id))![0].Version == 2 && (await skills.List(character.Id, player.Id))![0].Version == 2, "independent revisions persist after reload");
        }

        await using (var probe = context())
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probe.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            var count = await probe.CharacterInventoryItems.CountAsync(i => i.CharacterId == character.Id);
            foreach (var operation in new[] { "create", "update", "delete" })
            {
                try
                {
                    await using var failing = new AppDbContext(options); var commands = new ManageCharacterInventory(new EfCharacterInventoryStore(failing));
                    if (operation == "create") await commands.Create(character.Id, player.Id, itemValues);
                    else if (operation == "update") await commands.Update(character.Id, item.Id, player.Id, 2, itemValues with { Name = "Must roll back" });
                    else await commands.Delete(character.Id, item.Id, player.Id, 2);
                    throw new Exception("Expected forced asset failure.");
                }
                catch (InvalidOperationException error) when (error.Message == "Forced asset failure after SaveChanges") { }
            }
            var retained = await probe.CharacterInventoryItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
            Check(retained.Name == "Rope" && retained.Version == 2 && await probe.CharacterInventoryItems.CountAsync(i => i.CharacterId == character.Id) == count,
                "create/update/delete roll back even after SaveChanges when commit fails");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterAbilityStore(pending); var access = (await store.LoadAccess(character.Id, player.Id, default))!;
            var before = (await store.Find(character.Id, ability.Id, player.Id, default))!;
            await using (var change = context())
                await change.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == player.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "spectator"));
            Check((await store.Delete(access, before, player.Id, default)).Failure == CharacterAssetFailure.Forbidden &&
                await pending.CharacterAbilities.AnyAsync(a => a.Id == ability.Id), "permission revoked between read and delete preserves the ability");
            Check((await store.Create(access, abilityValues.Normalize(), player.Id, default)).Failure == CharacterAssetFailure.Forbidden, "commit-time authorization protects creation too");
        }
        await using (var db = context())
        {
            var items = new ManageCharacterInventory(new EfCharacterInventoryStore(db)); var skills = new ManageCharacterAbilities(new EfCharacterAbilityStore(db));
            Check((await items.Delete(character.Id, item.Id, owner.Id, 2)).Failure is null &&
                (await skills.Delete(character.Id, ability.Id, owner.Id, 2)).Failure is null, "master can delete at current child revision");
            Check((await db.Characters.SingleAsync(c => c.Id == character.Id)).Version == 1 &&
                !await db.CharacterRevisions.AnyAsync(r => r.CharacterId == character.Id), "child changes do not alter sheet revision/history contract");
        }
        Console.WriteLine("Character assets PostgreSQL: scoped IDs, roles, revisioned CRUD, concurrent winners/capacity, post-save rollback and permission revocation passed.");
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw new InvalidOperationException("Forced asset failure after SaveChanges");
    }
}
