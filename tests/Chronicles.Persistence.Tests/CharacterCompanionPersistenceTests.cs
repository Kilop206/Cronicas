using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class CharacterCompanionPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Companion PostgreSQL: " + label); }
        var master = new User { Email = "companion-master@example.test", DisplayName = "Master", PasswordHash = "fixture" };
        var player = new User { Email = "companion-player@example.test", DisplayName = "Player", PasswordHash = "fixture" };
        var spectator = new User { Email = "companion-spectator@example.test", DisplayName = "Spectator", PasswordHash = "fixture" };
        var campaign = new Campaign { Name = "Companions", OwnerId = master.Id };
        var character = new Character { Name = "Public character", CampaignId = campaign.Id, OwnerId = player.Id, FieldsJson = "{\"notes\":\"not part of OBS\"}" };
        var initialRace = new Character { Name = "Initial notes", CampaignId = campaign.Id, OwnerId = player.Id };
        await using (var db = context())
        {
            db.Users.AddRange(master, player, spectator); db.Campaigns.Add(campaign); db.Characters.AddRange(character, initialRace);
            foreach (var (user, role) in new[] { (master.Id, "owner"), (player.Id, "player"), (spectator.Id, "spectator") })
                db.CampaignMemberships.Add(new() { CampaignId = campaign.Id, UserId = user, Role = role });
            await db.SaveChangesAsync();
        }
        async Task<CharacterAssetResult<CharacterFavoriteDocument>> Favorite(bool enabled, Guid actor)
        {
            await using var db = context(); return await new ManageCharacterFavorites(new EfCharacterCompanionStore(db)).Set(character.Id, actor, enabled);
        }
        var added = await Task.WhenAll(Favorite(true, player.Id), Favorite(true, player.Id));
        Check(added.All(r => r.Failure is null), "simultaneous idempotent favorites succeed");
        await using (var db = context())
        {
            var store = new EfCharacterCompanionStore(db);
            Check(await db.CharacterFavorites.CountAsync(f => f.CharacterId == character.Id && f.UserId == player.Id) == 1, "one favorite row per actor");
            Check((await store.ReadFavorite(character.Id, master.Id, default)) is { Favorite: false }, "favorites remain personal");
            Check(await store.ReadFavorite(character.Id, Guid.NewGuid(), default) is null, "favorite query rejects non-member");
            Check((await new ManageCharacterFavorites(store).Set(character.Id, spectator.Id, true)).Failure is null, "spectator can favorite");
            var notes = new ManageCharacterSecrets(store);
            Check(await notes.Read(character.Id, player.Id) is null && await notes.Read(character.Id, spectator.Id) is null, "private notes require master role even for sheet owner");
            Check((await notes.Read(character.Id, master.Id)) is { Notes: "", Revision: 1, UpdatedAt: null }, "empty secret legacy contract preserved");
            Check((await notes.Save(character.Id, master.Id, new("  Plan  ", 1))).Value is { Notes: "Plan", Revision: 1 }, "first secret persisted without changing legacy revision");
            Check((await notes.Save(character.Id, master.Id, new("Stale first draft", 1, false))).Failure == CharacterAssetFailure.Conflict,
                "new clients distinguish empty notes from first persisted revision");
            Check((await notes.Save(character.Id, master.Id, new("Current", 1))).Value?.Revision == 2, "later save advances revision");
        }
        async Task<CharacterAssetResult<CharacterSecretDocument>> SaveNotes(string text)
        {
            await using var db = context(); return await new ManageCharacterSecrets(new EfCharacterCompanionStore(db)).Save(character.Id, master.Id, new(text, 2));
        }
        var edits = await Task.WhenAll(SaveNotes("First"), SaveNotes("Second"));
        Check(edits.Count(r => r.Failure is null) == 1 && edits.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1, "existing notes have one concurrent winner");
        CharacterAssetAccess initialAccess; CharacterSecretDocument initialBefore;
        await using (var db = context())
        { var store = new EfCharacterCompanionStore(db); initialAccess = (await store.LoadAccess(initialRace.Id, master.Id, default))!; initialBefore = (await store.ReadSecret(initialRace.Id, master.Id, default))!; }
        async Task<CharacterAssetResult<CharacterSecretDocument>> CreateNotes(string text)
        {
            await using var db = context(); return await new EfCharacterCompanionStore(db).SaveSecret(initialAccess, initialBefore, new(text, 1), master.Id, default);
        }
        var creations = await Task.WhenAll(CreateNotes("Initial A"), CreateNotes("Initial B"));
        Check(creations.Count(r => r.Failure is null) == 1 && creations.Count(r => r.Failure == CharacterAssetFailure.Conflict) == 1,
            "two prepared absent snapshots cannot both create private notes");
        string key; string hash;
        await using (var db = context())
        {
            var store = new EfCharacterCompanionStore(db); var obs = new ManageCharacterOverlay(store, new CryptographicOverlayKeyGenerator(), RpgPluginRegistry.Default);
            Check((await obs.Rotate(character.Id, master.Id)).Failure == CharacterAssetFailure.NotFound, "master cannot rotate another owner's capability");
            key = (await obs.Rotate(character.Id, player.Id)).Value!.Key; hash = OverlayCapability.Hash(key);
            Check(OverlayCapability.IsValid(key) && (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).OverlayKeyHash == hash && key != hash,
                "cryptographic raw capability never stored");
            var old = key; key = (await obs.Rotate(character.Id, player.Id)).Value!.Key; hash = OverlayCapability.Hash(key);
            Check(await obs.Read(old) is null && await obs.Read(key.ToUpperInvariant()) is { Name: "Public character" }, "rotation revokes old capability and accepts normalized new key");
        }
        var removed = await Task.WhenAll(Favorite(false, player.Id), Favorite(false, player.Id));
        Check(removed.All(r => r.Failure is null), "simultaneous favorite removal is idempotent");
        await using (var probe = context())
        {
            var events = await probe.OutboxMessages.CountAsync(); var notes = await probe.CharacterSecrets.AsNoTracking().SingleAsync(s => s.CharacterId == character.Id);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probe.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            foreach (var operation in new[] { "favorite", "secret", "overlay" })
            {
                try
                {
                    await using var fail = new AppDbContext(options); var store = new EfCharacterCompanionStore(fail);
                    if (operation == "favorite") await new ManageCharacterFavorites(store).Set(character.Id, player.Id, true);
                    else if (operation == "secret") await new ManageCharacterSecrets(store).Save(character.Id, master.Id, new("Must roll back", notes.Revision));
                    else await new ManageCharacterOverlay(store, new CryptographicOverlayKeyGenerator(), RpgPluginRegistry.Default).Rotate(character.Id, player.Id);
                    throw new Exception("Expected forced companion failure.");
                }
                catch (InvalidOperationException error) when (error.Message == "Forced companion failure after SaveChanges") { }
            }
            Check(!await probe.CharacterFavorites.AnyAsync(f => f.CharacterId == character.Id && f.UserId == player.Id) &&
                (await probe.CharacterSecrets.AsNoTracking().SingleAsync(s => s.CharacterId == character.Id)).Notes == notes.Notes &&
                (await probe.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).OverlayKeyHash == hash && await probe.OutboxMessages.CountAsync() == events,
                "failed favorite/secret/rotation preserve all state and outbox");
            var invalidOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(probe.Database.GetConnectionString())
                .AddInterceptors(new FailAfterSave(new DbUpdateException("Unexpected companion database error"))).Options;
            try
            {
                await using var invalid = new AppDbContext(invalidOptions);
                await new ManageCharacterSecrets(new EfCharacterCompanionStore(invalid)).Save(character.Id, master.Id, new("Fail", notes.Revision));
                throw new Exception("Unexpected database error was hidden.");
            }
            catch (DbUpdateException error) when (error.Message == "Unexpected companion database error") { }
        }
        await using (var pending = context())
        {
            var store = new EfCharacterCompanionStore(pending);
            var beforeOwner = (await store.LoadAccess(character.Id, player.Id, default))!;
            var beforeMaster = (await store.LoadAccess(character.Id, master.Id, default))!;
            var notes = (await store.ReadSecret(character.Id, master.Id, default))!;
            await using (var change = context())
            {
                await change.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == player.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "spectator"));
                await change.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == master.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "player"));
            }
            Check((await store.RotateOverlay(beforeOwner, OverlayCapability.Hash(new string('d', 64)), player.Id, default)).Failure == CharacterAssetFailure.NotFound &&
                (await store.SaveSecret(beforeMaster, notes, new("Denied", notes.Revision), master.Id, default)).Failure == CharacterAssetFailure.NotFound,
                "role revoked before commit blocks rotation and private writes");
            Check((await pending.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).OverlayKeyHash == hash, "revoked rotation preserves existing key");
            await using (var change = context())
                await change.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == spectator.Id).ExecuteDeleteAsync();
            Check((await new ManageCharacterFavorites(store).Set(character.Id, spectator.Id, false)).Failure == CharacterAssetFailure.NotFound, "removed member cannot change old favorite");
            Check((await pending.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).Version == 1 &&
                !await pending.CharacterRevisions.AnyAsync(r => r.CharacterId == character.Id), "companions retain independent sheet history contract");
        }
        Console.WriteLine("Companion PostgreSQL: personal/idempotent favorites, master-only notes, concurrent writes, hash-only OBS rotation, revoked access and atomic rollback passed.");
    }

    private sealed class FailAfterSave(Exception? error = null) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) =>
            throw error ?? new InvalidOperationException("Forced companion failure after SaveChanges");
    }
}
