using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

public static class CharacterSystemPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("System versions PostgreSQL: " + label); }
        var master = new User { Email = "version-master@example.test", DisplayName = "Master", PasswordHash = "fixture" };
        var player = new User { Email = "version-player@example.test", DisplayName = "Player", PasswordHash = "fixture" };
        var schema = new SystemDefinition([new("score", "Original", "number", "Stats", 0, 20, null),
            new("double_score", "Double", "number", "Stats", 0, 40, null, Formula: "score * 2")], null,
            NpcTemplates: [new("private_template", "Private NPC", Sheet: new())]);
        var source = new RpgSystem { Name = "Original template", OwnerId = master.Id, SchemaJson = JsonSerializer.Serialize(schema) };
        var otherSystem = new RpgSystem { Name = "Other", OwnerId = master.Id, SchemaJson = JsonSerializer.Serialize(new SystemDefinition([], null)) };
        var campaign = new Campaign { Name = "Version fixture", OwnerId = master.Id, SystemId = source.Id };
        var first = new Character { Name = "Pinned A", OwnerId = player.Id, CampaignId = campaign.Id, FieldsJson = "{\"score\":2,\"double_score\":4}" };
        var second = new Character { Name = "Pinned B", OwnerId = player.Id, CampaignId = campaign.Id, FieldsJson = first.FieldsJson };
        var legacy = new Character { Name = "Legacy", OwnerId = player.Id, CampaignId = campaign.Id, FieldsJson = first.FieldsJson };
        await using (var db = context())
        {
            db.Users.AddRange(master, player); db.RpgSystems.AddRange(source, otherSystem); db.Campaigns.Add(campaign); db.Characters.AddRange(first, second, legacy);
            db.CampaignMemberships.AddRange(new() { CampaignId = campaign.Id, UserId = master.Id, Role = "owner" }, new() { CampaignId = campaign.Id, UserId = player.Id, Role = "player" });
            await db.SaveChangesAsync();
            Check((await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(first.Id, player.Id, 1, source.Id, 1)).Failure == CharacterWriteFailure.Forbidden, "player cannot pin own sheet");
        }
        async Task<CharacterWriteResponse> Pin(Guid id)
        { await using var db = context(); return await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(id, master.Id, 1, source.Id, 1); }
        var pins = await Task.WhenAll(Pin(first.Id), Pin(second.Id));
        Check(pins.All(r => r.Failure is null) && pins[0].Character!.SystemVersionId == pins[1].Character!.SystemVersionId, "parallel captures reuse one immutable version");
        var versionId = pins[0].Character!.SystemVersionId!.Value;
        await using (var db = context())
        {
            Check(await db.RpgSystemVersions.CountAsync(v => v.RpgSystemId == source.Id) == 1, "one version per fingerprint");
            Check((await db.Characters.SingleAsync(c => c.Id == legacy.Id)).SystemVersionId is null, "legacy character is never backfilled");
            var read = await new EfCharacterSystemStore(db).ReadSystem(first.Id, player.Id, default);
            Check(read.System is { Name: "Original template", SystemVersionId: not null } && read.System.NpcTemplates.Length == 0, "public pinned definition excludes private NPC catalog");
            var original = await db.RpgSystems.SingleAsync(s => s.Id == source.Id);
            original.Name = "Changed template"; original.SchemaJson = JsonSerializer.Serialize(schema with { Revision = 2, Fields = [schema.Fields[0] with { Label = "Changed" }, schema.Fields[1] with { Formula = "score * 3" }] });
            await db.SaveChangesAsync();
        }
        await using (var db = context())
        {
            var writer = new EfCharacterWriteStore(db);
            var pinned = (await writer.Load(first.Id, player.Id, default))!;
            var unbound = (await writer.Load(legacy.Id, player.Id, default))!;
            Check(pinned.System!.Revision == 1 && unbound.System!.Revision == 2, "effective schemas differ after source edit");
            var saved = await new SaveCharacter(writer, RpgPluginRegistry.Default).Execute(first.Id, player.Id,
                pinned.State with { Fields = new(pinned.State.Fields!) { ["score"] = JsonSerializer.SerializeToElement(3) } });
            Check(saved.Failure is null && saved.Character!.State.Fields!["double_score"].GetInt32() == 6, "save calculates with pinned formula");
            var changed = await db.Campaigns.SingleAsync(c => c.Id == campaign.Id); changed.SystemId = otherSystem.Id; await db.SaveChangesAsync();
        }
        await using (var db = context())
        {
            var store = new EfCharacterSystemStore(db); var bound = (await store.Load(first.Id, player.Id, default))!;
            Check(bound.SystemId == source.Id && bound.SystemVersionId == versionId && (await store.ReadSystem(first.Id, player.Id, default)).System!.Name == "Original template", "campaign replacement preserves pinned identity and display");
            Check((await store.Load(legacy.Id, player.Id, default))!.SystemId == otherSystem.Id, "legacy follows campaign replacement");
            var copy = await new DuplicateCharacter(new EfCharacterLifecycleStore(db)).Execute(first.Id, player.Id);
            Check(copy.Character?.SystemVersionId == versionId && copy.Character.State.Version == 1, "copy preserves immutable binding");
            var history = (await new ReadCharacterLifecycle(new EfCharacterLifecycleStore(db)).History(first.Id, player.Id))!;
            var restored = await new RestoreCharacter(new EfCharacterLifecycleStore(db), new EfCharacterWriteStore(db), RpgPluginRegistry.Default)
                .Execute(first.Id, history[0].Id, bound.State.Version!.Value, player.Id);
            Check(restored.Failure is null && restored.Character!.SystemVersionId == versionId, "state restoration does not silently replace binding");
        }
        await using (var db = context())
        {
            var immutable = await db.RpgSystemVersions.SingleAsync(v => v.Id == versionId); immutable.Name = "Forbidden";
            try { await db.SaveChangesAsync(); throw new Exception("Mutable archive accepted."); }
            catch (InvalidOperationException error) when (error.Message == "Captured RPG system versions are immutable.") { }
        }
        await using (var db = context())
        {
            try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE rpg_system_versions SET \"Name\" = 'Forbidden SQL' WHERE \"Id\" = {versionId}"); throw new Exception("SQL update accepted."); }
            catch (PostgresException error) when (error.SqlState == "P0001") { }
            try { await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM rpg_system_versions WHERE \"Id\" = {versionId}"); throw new Exception("SQL deletion accepted."); }
            catch (PostgresException error) when (error.SqlState == "P0001") { }
            try { await db.GetService<IMigrator>().MigrateAsync("20261104172257_IntegrationOutbox"); throw new Exception("Destructive downgrade accepted."); }
            catch (PostgresException error) when (error.SqlState == "P0001") { }
            Check(await db.RpgSystemVersions.AnyAsync(v => v.Id == versionId) && await db.Characters.AnyAsync(c => c.Id == first.Id && c.SystemVersionId == versionId), "immutability and rollback guards preserve captured data");
        }
        var rollbackCampaign = new Campaign { Name = "Rollback pin", OwnerId = master.Id, SystemId = otherSystem.Id };
        var rollbackCharacter = new Character { Name = "Rollback pin", CampaignId = rollbackCampaign.Id, OwnerId = player.Id };
        await using (var db = context())
        {
            db.Campaigns.Add(rollbackCampaign); db.Characters.Add(rollbackCharacter); db.CampaignMemberships.Add(new() { CampaignId = rollbackCampaign.Id, UserId = master.Id, Role = "owner" }); await db.SaveChangesAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            var events = await db.OutboxMessages.CountAsync();
            try
            { await using var fail = new AppDbContext(options); await new PinCharacterSystem(new EfCharacterSystemStore(fail)).Execute(rollbackCharacter.Id, master.Id, 1, otherSystem.Id, 1); throw new Exception("Expected forced pin failure."); }
            catch (InvalidOperationException error) when (error.Message == "Forced pin failure") { }
            Check(!await db.RpgSystemVersions.AnyAsync(v => v.RpgSystemId == otherSystem.Id) &&
                (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == rollbackCharacter.Id)).SystemVersionId is null &&
                !await db.CharacterRevisions.AnyAsync(r => r.CharacterId == rollbackCharacter.Id) && await db.OutboxMessages.CountAsync() == events, "failed pin rolls back archive, history, binding and outbox");
        }
        // Rules and encounter projections must keep the same edition even after the campaign changes.
        var op = new SystemDefinition([new("nex", "NEX", "number", "Agent", 5, 99, null), new("estado_regras", "State", "table", "Rules", null, null, null,
            Columns: [new("chave", "Key", "text"), new("valor", "Value", "number")])], null, "ordem_paranormal", 7, "1.3");
        await using (var db = context())
        {
            var opSystem = new RpgSystem { Name = "OP", OwnerId = master.Id, SchemaJson = JsonSerializer.Serialize(op) }; db.RpgSystems.Add(opSystem);
            var opCampaign = new Campaign { Name = "OP pin", OwnerId = master.Id, SystemId = opSystem.Id }; db.Campaigns.Add(opCampaign);
            var agent = new Character { Name = "Agent", CampaignId = opCampaign.Id, OwnerId = player.Id, FieldsJson = "{\"nex\":35}" }; db.Characters.Add(agent);
            db.CampaignMemberships.AddRange(new() { CampaignId = opCampaign.Id, UserId = master.Id, Role = "owner" }, new() { CampaignId = opCampaign.Id, UserId = player.Id, Role = "player" });
            await db.SaveChangesAsync();
            Check((await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(agent.Id, master.Id, 1, opSystem.Id, 1)).Failure is null, "OP version pinned");
            opCampaign.SystemId = otherSystem.Id; await db.SaveChangesAsync();
            var damage = await new ExecuteCharacterRule(new EfCharacterRuleStore(db), RpgPluginRegistry.Default, new FixedRandom()).Execute(agent.Id, player.Id, 2, new("damage", 3));
            Check(damage.Failure is null && damage.Character!.State.Health == 7, "authoritative rules use pinned edition: " + JsonSerializer.Serialize(damage));
            Check((await CharacterEncounterConditions.Definition(db, opCampaign.Id, agent.Id))?.TemplateId == "ordem_paranormal", "encounter resolves character edition");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterSystemStore(pending); var before = (await store.Load(rollbackCharacter.Id, master.Id, default))!;
            await using (var change = context())
            {
                var changed = await change.RpgSystems.SingleAsync(s => s.Id == otherSystem.Id);
                changed.SchemaJson = JsonSerializer.Serialize(new SystemDefinition([], null, Revision: 2)); await change.SaveChangesAsync();
            }
            Check((await store.Pin(before, master.Id, default)).Failure == CharacterWriteFailure.Conflict, "template changed after validation prevents pin");
            before = (await store.Load(rollbackCharacter.Id, master.Id, default))!;
            await using (var change = context())
                await change.CampaignMemberships.Where(m => m.CampaignId == rollbackCampaign.Id && m.UserId == master.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "spectator"));
            Check((await store.Pin(before, master.Id, default)).Failure == CharacterWriteFailure.Forbidden &&
                !await pending.RpgSystemVersions.AnyAsync(v => v.RpgSystemId == otherSystem.Id), "revoked master role prevents archive and binding at commit");
        }
        Console.WriteLine("System versions PostgreSQL: concurrent deduplication, legacy fallback, pinned formulas/rules/copies/history, immutable EF/SQL, guarded downgrade and atomic rollback passed.");
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    { public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default) => throw new InvalidOperationException("Forced pin failure"); }
    private sealed class FixedRandom : IGameRandom { public int Draw(int sides) => sides; }
}
