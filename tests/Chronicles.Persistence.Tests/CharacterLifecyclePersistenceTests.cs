using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class CharacterLifecyclePersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Lifecycle PostgreSQL: " + label); }
        var gm = new User { Email = "lifecycle-gm@example.test", PasswordHash = "fixture", DisplayName = "History GM" };
        var player = new User { Email = "lifecycle-player@example.test", PasswordHash = "fixture", DisplayName = "Player" };
        var other = new User { Email = "lifecycle-other@example.test", PasswordHash = "fixture", DisplayName = "Other" };
        var spectator = new User { Email = "lifecycle-spectator@example.test", PasswordHash = "fixture", DisplayName = "Spectator" };
        var schema = new SystemDefinition([new("score", "Score", "number", "Stats", 0, 20, null),
            new("double_score", "Double", "number", "Stats", 0, 40, null, Formula: "score * 2"),
            new("gm_note", "GM", "text", "Notes", null, null, null, EditRole: "gm")], null);
        var system = new RpgSystem { Name = "Lifecycle", OwnerId = gm.Id, SchemaJson = JsonSerializer.Serialize(schema) };
        var campaign = new Campaign { Name = "Lifecycle", OwnerId = gm.Id, SystemId = system.Id };
        var source = new Character { Name = "Original", OwnerId = player.Id, CampaignId = campaign.Id, Health = 15, MaxHealth = 30,
            OverlayKeyHash = new string('c', 64), FieldsJson = "{\"score\":2,\"double_score\":4,\"gm_note\":\"A\",\"retired_field\":{\"text\":\"archive\"}}" };
        var second = new Character { Name = "Other", OwnerId = other.Id, CampaignId = campaign.Id };
        await using (var db = context())
        {
            db.Users.AddRange(gm, player, other, spectator); db.RpgSystems.Add(system); db.Campaigns.Add(campaign); db.Characters.AddRange(source, second);
            foreach (var (user, role) in new[] { (gm.Id, "owner"), (player.Id, "player"), (other.Id, "player"), (spectator.Id, "spectator") })
                db.CampaignMemberships.Add(new() { CampaignId = campaign.Id, UserId = user, Role = role });
            db.CharacterSecrets.Add(new() { CharacterId = source.Id, Notes = "Do not copy", Revision = 3 });
            db.CharacterFavorites.Add(new() { CharacterId = source.Id, UserId = gm.Id });
            db.CharacterInventoryItems.Add(new() { CharacterId = source.Id, Name = "Fixture item", Quantity = 2, Weight = 1.5m, Notes = "Keep", Version = 4 });
            db.CharacterAbilities.Add(new() { CharacterId = source.Id, Name = "Fixture ability", Description = "Description", Cost = "2", Effect = "Effect",
                Duration = "Scene", Limitations = "Limit", Recharge = "Rest", RollExpression = "1d6+2", Version = 7 });
            await db.SaveChangesAsync();
        }
        CharacterSheetSnapshot current;
        async Task<CharacterSheetSnapshot> Save(Guid id, Action<Dictionary<string, JsonElement>> change, int health)
        {
            await using var db = context(); var store = new EfCharacterWriteStore(db); var before = (await store.Load(id, gm.Id, default))!;
            var fields = new Dictionary<string, JsonElement>(before.State.Fields!); change(fields);
            var result = await new SaveCharacter(store, RpgPluginRegistry.Default).Execute(id, gm.Id, before.State with { Fields = fields, Health = health });
            Check(result.Failure is null, "fixture save succeeds"); return result.Character!;
        }
        current = await Save(source.Id, f => f["gm_note"] = JsonSerializer.SerializeToElement("B"), 8);
        current = await Save(source.Id, f => f.Remove("retired_field"), 7);
        await Save(second.Id, _ => { }, 9);
        Guid revision1; Guid revision2;
        await using (var db = context())
        {
            var store = new EfCharacterLifecycleStore(db); var queries = new ReadCharacterLifecycle(store);
            var list = (await queries.Campaign(campaign.Id, other.Id))!;
            Check(list.Length == 2 && !list.Single(c => c.Id == source.Id).CanEdit && list.Single(c => c.Id == second.Id).CanEdit, "list permissions per owner");
            Check((await queries.Campaign(campaign.Id, spectator.Id))!.All(c => !c.CanEdit), "spectator list stays read-only");
            Check(await queries.Campaign(campaign.Id, Guid.NewGuid()) is null, "non-member list denied");
            var history = (await queries.History(source.Id, player.Id))!;
            Check(history.Select(r => r.Version).SequenceEqual([2, 1]) && history.All(r => r.EditedBy == "History GM"), "history order, editor and metadata");
            revision2 = history[0].Id; revision1 = history[1].Id;
            Check(await queries.History(source.Id, other.Id) is null && await queries.History(source.Id, spectator.Id) is null, "history hidden from other players and spectators");
            var foreign = (await store.ListRevisions(second.Id, gm.Id, 100, default))![0].Id;
            Check(await store.LoadRevision(source.Id, foreign, gm.Id, default) is null, "cross-character revision denied");
            Check((await new RestoreCharacter(store, new EfCharacterWriteStore(db), RpgPluginRegistry.Default).Execute(source.Id, revision1, 3, player.Id)).Failure == CharacterWriteFailure.Invalid,
                "player cannot revert a master field through old history");
        }
        await using (var db = context())
        {
            var result = await new RestoreCharacter(new EfCharacterLifecycleStore(db), new EfCharacterWriteStore(db), RpgPluginRegistry.Default)
                .Execute(source.Id, revision2, 3, player.Id);
            Check(result.Failure is null && result.Character!.State.Version == 4 && result.Character.State.Health == 8, "restore appends a revision");
            current = result.Character!;
            Check(current.State.Fields!["retired_field"].GetProperty("text").GetString() == "archive", "historical field restored after omission from current state");
            Check(await db.CharacterRevisions.CountAsync(r => r.CharacterId == source.Id) == 3, "restore preserves prior history");
        }
        foreach (var actor in new[] { player.Id, gm.Id })
        {
            await using var db = context();
            var result = await new DuplicateCharacter(new EfCharacterLifecycleStore(db)).Execute(source.Id, actor);
            Check(result.Failure is null && result.Character!.OwnerId == actor && result.Character.State.Version == 1, "duplicate has authenticated owner and new revision");
            var copied = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == result.Character!.Id);
            Check(copied.FieldsJson == (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == source.Id)).FieldsJson && copied.Health == 8, "copy retains exact persisted state");
            Check(copied.OverlayKeyHash is null && !await db.CharacterSecrets.AnyAsync(s => s.CharacterId == copied.Id) &&
                !await db.CharacterFavorites.AnyAsync(f => f.CharacterId == copied.Id) && !await db.CharacterRevisions.AnyAsync(r => r.CharacterId == copied.Id),
                "no secrets, OBS key, favorites or source history copied");
            var item = await db.CharacterInventoryItems.SingleAsync(i => i.CharacterId == copied.Id);
            var ability = await db.CharacterAbilities.SingleAsync(a => a.CharacterId == copied.Id);
            Check(item.Version == 1 && item.Quantity == 2 && item.Weight == 1.5m && item.Notes == "Keep", "inventory copied independently");
            Check(ability.Version == 1 && ability.Description == "Description" && ability.Cost == "2" && ability.Effect == "Effect" && ability.Duration == "Scene" &&
                ability.Limitations == "Limit" && ability.Recharge == "Rest" && ability.RollExpression == "1d6+2", "all ability fields copied");
        }
        await using (var probe = context())
        {
            var counts = (Characters: await probe.Characters.CountAsync(), Items: await probe.CharacterInventoryItems.CountAsync(),
                Abilities: await probe.CharacterAbilities.CountAsync(), Events: await probe.OutboxMessages.CountAsync());
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probe.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            try
            {
                await using var fail = new AppDbContext(options);
                await new DuplicateCharacter(new EfCharacterLifecycleStore(fail)).Execute(source.Id, player.Id);
                throw new Exception("Expected forced transactional failure.");
            }
            catch (InvalidOperationException error) when (error.Message == "Forced lifecycle failure after SaveChanges") { }
            Check(await probe.Characters.CountAsync() == counts.Characters && await probe.CharacterInventoryItems.CountAsync() == counts.Items &&
                await probe.CharacterAbilities.CountAsync() == counts.Abilities && await probe.OutboxMessages.CountAsync() == counts.Events,
                "failed copy rolls back character, children and outbox even after SaveChanges");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterLifecycleStore(pending); var before = (await store.Load(source.Id, player.Id, default))!;
            await Save(source.Id, _ => { }, 6);
            Check((await store.Duplicate(before, before.State with { Name = "Stale copy" }, player.Id, default)).Failure == CharacterWriteFailure.Conflict, "changed source prevents stale duplication");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterLifecycleStore(pending); var before = (await store.Load(source.Id, player.Id, default))!;
            await using (var change = context())
            {
                var changed = await change.RpgSystems.SingleAsync(s => s.Id == system.Id);
                changed.SchemaJson = JsonSerializer.Serialize(schema with { Revision = 2 });
                await change.SaveChangesAsync();
            }
            Check((await store.Duplicate(before, before.State, player.Id, default)).Failure == CharacterWriteFailure.Conflict,
                "changed template prevents copy validated against an obsolete context");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterLifecycleStore(pending); var before = (await store.Load(source.Id, player.Id, default))!;
            var count = await pending.Characters.CountAsync();
            await using (var change = context())
                await change.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == player.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "spectator"));
            Check((await store.Duplicate(before, before.State, player.Id, default)).Failure == CharacterWriteFailure.NotFound &&
                await pending.Characters.CountAsync() == count, "permission revoked before copy cannot create a character");
        }
        Console.WriteLine("Character lifecycle PostgreSQL: list/history authorization, master fields, retired-field restore, independent copies, source conflicts and atomic child/outbox rollback passed.");
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw new InvalidOperationException("Forced lifecycle failure after SaveChanges");
    }
}
