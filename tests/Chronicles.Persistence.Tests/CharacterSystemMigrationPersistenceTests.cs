using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class CharacterSystemMigrationPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Migration PostgreSQL: " + label); }
        var designer = new User { Email = "migration-designer@example.test", DisplayName = "Designer", PasswordHash = "fixture" };
        var master = new User { Email = "migration-master@example.test", DisplayName = "Master", PasswordHash = "fixture" };
        var player = new User { Email = "migration-player@example.test", DisplayName = "Player", PasswordHash = "fixture" };
        var schema = new SystemDefinition([new("score", "Original", "number", "Stats", 0, 20, null)], null);
        var source = new RpgSystem { Name = "Migration source", OwnerId = designer.Id, SchemaJson = JsonSerializer.Serialize(schema) };
        var campaign = new Campaign { Name = "Migration fixture", OwnerId = master.Id, SystemId = source.Id };
        var character = new Character { Name = "Migrant", OwnerId = player.Id, CampaignId = campaign.Id, Health = 3, MaxHealth = 20,
            FieldsJson = "{\"score\":4,\"legacy_notes\":\"preserve\"}", OverlayKeyHash = new string('a', 64) };
        var item = new CharacterInventoryItem { CharacterId = character.Id, Name = "Keep inventory" };
        Guid original;
        await using (var db = context())
        {
            db.Users.AddRange(designer, master, player); db.RpgSystems.Add(source); db.Campaigns.Add(campaign); db.Characters.Add(character); db.CharacterInventoryItems.Add(item);
            db.CampaignMemberships.AddRange(new() { CampaignId = campaign.Id, UserId = master.Id, Role = "owner" }, new() { CampaignId = campaign.Id, UserId = player.Id, Role = "player" });
            await db.SaveChangesAsync();
            var pin = await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(character.Id, master.Id, 1, source.Id, 1);
            original = pin.Character!.SystemVersionId!.Value;
        }
        var schema2 = schema with { Revision = 2, Fields = [schema.Fields[0] with { Key = "renamed_score" },
            new("derived", "Derived", "number", "Stats", 0, 100, null, Formula: "renamed_score * 3")] };
        async Task UpdateSource(SystemDefinition definition)
        {
            await using var db = context(); var s = await db.RpgSystems.SingleAsync(s => s.Id == source.Id);
            s.SchemaJson = JsonSerializer.Serialize(definition); await db.SaveChangesAsync();
        }
        await UpdateSource(schema2);
        SystemMigrationInput input; SystemMigrationPreview preview;
        await using (var db = context())
        {
            var service = new MigrateCharacterSystem(new EfCharacterSystemMigrationStore(db, RpgPluginRegistry.Default), RpgPluginRegistry.Default);
            Check((await service.Targets(character.Id, player.Id)).Failure == CharacterWriteFailure.Forbidden, "player cannot enumerate private schemas");
            var target = (await service.Targets(character.Id, master.Id)).Options!.Targets.Single(t => t.Current);
            input = new(2, original, null, 2, target.ContentHash, new() { ["score"] = "renamed_score" });
            preview = (await service.Preview(character.Id, master.Id, input)).Preview!;
            Check(preview.CanApply && await db.CharacterRevisions.CountAsync(r => r.CharacterId == character.Id) == 1 &&
                await db.RpgSystemVersions.CountAsync(v => v.RpgSystemId == source.Id) == 1, "preview never writes history or archives");
        }
        async Task<CharacterWriteResponse> Apply(SystemMigrationInput plan, string hash)
        { await using var db = context(); return await new MigrateCharacterSystem(new EfCharacterSystemMigrationStore(db, RpgPluginRegistry.Default), RpgPluginRegistry.Default).Apply(character.Id, master.Id, plan, hash); }
        await using (var db = context())
        {
            var events = await db.OutboxMessages.CountAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailMigrationAfterSave()).Options;
            try
            {
                await using var failure = new AppDbContext(options);
                await new MigrateCharacterSystem(new EfCharacterSystemMigrationStore(failure, RpgPluginRegistry.Default), RpgPluginRegistry.Default)
                    .Apply(character.Id, master.Id, input, preview.PreviewHash!);
                throw new Exception("Expected forced migration failure.");
            }
            catch (InvalidOperationException error) when (error.Message == "Forced migration failure") { }
            Check(await db.RpgSystemVersions.CountAsync(v => v.RpgSystemId == source.Id) == 1 &&
                await db.CharacterRevisions.CountAsync(r => r.CharacterId == character.Id) == 1 &&
                (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).SystemVersionId == original &&
                await db.OutboxMessages.CountAsync() == events, "post-save failure rolls back archive, receipt, binding, state and outbox");
        }
        var raced = await Task.WhenAll(Apply(input, preview.PreviewHash!), Apply(input, preview.PreviewHash!));
        Check(raced.Count(r => r.Failure is null) == 1 && raced.Count(r => r.Failure == CharacterWriteFailure.Conflict) == 1, "concurrent confirmation commits exactly once");
        var migrated = raced.Single(r => r.Failure is null).Character!;
        Check(migrated.State.Version == 3 && migrated.SystemVersionId != original && migrated.State.Fields!["derived"].GetInt32() == 12 &&
            migrated.State.Fields["score"].GetInt32() == 4 && migrated.State.Fields["legacy_notes"].GetString() == "preserve", "binding, calculation and legacy preservation atomic");
        await using (var db = context())
        {
            var saved = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
            Check(saved.Health == 3 && saved.MaxHealth == 20 && saved.OverlayKeyHash == character.OverlayKeyHash &&
                await db.CharacterInventoryItems.AnyAsync(i => i.Id == item.Id), "resources, OBS capability and inventory preserved");
            var revision = await db.CharacterRevisions.SingleAsync(r => r.CharacterId == character.Id && r.Version == 2);
            var snapshot = JsonSerializer.Deserialize<CharacterSnapshot>(revision.SnapshotJson)!;
            Check(snapshot.SystemVersionId == original && snapshot.Migration?.TargetVersionId == migrated.SystemVersionId &&
                snapshot.Migration?.PreviewHash == preview.PreviewHash && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(snapshot.FieldsJson),
                    JsonSerializer.Deserialize<JsonElement>(character.FieldsJson)), "before state and exact migration receipt stored together");
            var service = new MigrateCharacterSystem(new EfCharacterSystemMigrationStore(db, RpgPluginRegistry.Default), RpgPluginRegistry.Default);
            Check((await service.Targets(character.Id, master.Id)).Options!.History.Single().CharacterRevision == 3, "master can audit the committed migration");
            var old = (await service.Targets(character.Id, master.Id)).Options!.Targets.Single(t => t.VersionId == original && !t.Current);
            var reverse = new SystemMigrationInput(3, migrated.SystemVersionId!.Value, original, old.Revision, old.ContentHash);
            var review = (await service.Preview(character.Id, master.Id, reverse)).Preview!;
            Check(review.CanApply && (await service.Apply(character.Id, master.Id, reverse, review.PreviewHash!)).Failure is null, "explicit reverse migration reuses an immutable archive");
        }
        // A source update after review invalidates the current-target plan even though the bound sheet did not change.
        await using (var db = context())
        {
            var store = new EfCharacterSystemMigrationStore(db, RpgPluginRegistry.Default);
            var service = new MigrateCharacterSystem(store, RpgPluginRegistry.Default);
            var target = (await service.Targets(character.Id, master.Id)).Options!.Targets.Single(t => t.Current);
            input = new(4, original, null, target.Revision, target.ContentHash);
            preview = (await service.Preview(character.Id, master.Id, input)).Preview!;
            var before = (await store.Load(character.Id, master.Id, default))!;
            await using (var revoke = context())
            {
                (await revoke.CampaignMemberships.SingleAsync(m => m.CampaignId == campaign.Id && m.UserId == master.Id)).Role = "player";
                await revoke.SaveChangesAsync();
            }
            Check((await store.Commit(before, master.Id, input, preview.PreviewHash!, default)).Failure == CharacterWriteFailure.Forbidden,
                "master authorization rechecked under commit locks");
            await using (var restore = context())
            {
                (await restore.CampaignMemberships.SingleAsync(m => m.CampaignId == campaign.Id && m.UserId == master.Id)).Role = "owner";
                await restore.SaveChangesAsync();
            }
        }
        await UpdateSource(schema2 with { Revision = 3 });
        Check((await Apply(input, preview.PreviewHash!)).Failure == CharacterWriteFailure.Conflict, "changed destination invalidates preview");
        Guid privateArchive;
        await using (var db = context())
        {
            var another = new Character { Name = "Private future snapshot", CampaignId = campaign.Id, OwnerId = player.Id };
            db.Characters.Add(another); await db.SaveChangesAsync();
            var pin = await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(another.Id, master.Id, 1, source.Id, 3);
            privateArchive = pin.Character!.SystemVersionId!.Value;
            (await db.Campaigns.SingleAsync(c => c.Id == campaign.Id)).SystemId = null; await db.SaveChangesAsync();
        }
        await using (var db = context())
        {
            var service = new MigrateCharacterSystem(new EfCharacterSystemMigrationStore(db, RpgPluginRegistry.Default), RpgPluginRegistry.Default);
            var options = (await service.Targets(character.Id, master.Id)).Options!;
            Check(options.Targets.Any(t => t.VersionId == migrated.SystemVersionId) && !options.Targets.Any(t => t.VersionId == privateArchive || t.Current),
                "detached character sees its migration history, not private future schemas");
            Check((await service.Preview(character.Id, master.Id, input with { TargetVersionId = privateArchive })).Failure == CharacterWriteFailure.NotFound, "guessed inaccessible version ID hidden");
            Check(await db.CharacterRevisions.CountAsync(r => r.CharacterId == character.Id) == 3 &&
                (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id)).Version == 4, "failed previews and applies leave no history or partial changes");
        }
        Console.WriteLine("Assisted migration PostgreSQL: atomic concurrent confirmation/rollback, commit-time authorization, immutable archives, receipt/history, reverse workflow, stale destinations, preserved inventory/resources/OBS and private-source isolation passed.");
    }

    private sealed class FailMigrationAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default) =>
            throw new InvalidOperationException("Forced migration failure");
    }
}
