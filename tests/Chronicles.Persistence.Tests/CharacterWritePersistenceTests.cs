using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Chronicles.Domain.Events;
using Chronicles.Infrastructure.Characters;
using Microsoft.EntityFrameworkCore;

public static class CharacterWritePersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Character persistence: " + label); }
        var gm = new User { DisplayName = "GM", Email = "write-gm@example.test", PasswordHash = "fixture" };
        var player = new User { DisplayName = "Player", Email = "write-player@example.test", PasswordHash = "fixture" };
        var spectator = new User { DisplayName = "Spectator", Email = "write-spectator@example.test", PasswordHash = "fixture" };
        var schema = new SystemDefinition([
            new("score", "Score", "number", "Stats", 0, 20, null),
            new("double_score", "Double", "number", "Stats", 0, 40, null, Formula: "score * 2"),
            new("gm_note", "Master", "text", "Notes", null, null, null, EditRole: "gm")
        ], null);
        var system = new RpgSystem { Name = "Write fixture", OwnerId = gm.Id, SchemaJson = JsonSerializer.Serialize(schema) };
        var campaign = new Campaign { Name = "Write fixture", OwnerId = gm.Id, SystemId = system.Id };
        await using (var seed = context())
        {
            seed.Users.AddRange(gm, player, spectator); seed.RpgSystems.Add(system); seed.Campaigns.Add(campaign);
            foreach (var (id, role) in new[] { (gm.Id, "owner"), (player.Id, "player"), (spectator.Id, "spectator") })
                seed.CampaignMemberships.Add(new() { CampaignId = campaign.Id, UserId = id, Role = role });
            await seed.SaveChangesAsync();
        }
        var input = new CharacterSheetState("  Player character  ", null, null, 15, 30, 10, 20, 10, 20,
            new() { ["score"] = JsonSerializer.SerializeToElement(3) }, 900);
        CharacterSheetSnapshot created;
        await using (var db = context())
        {
            var result = await new CreateCharacter(new EfCharacterWriteStore(db), RpgPluginRegistry.Default).Execute(campaign.Id, player.Id, input);
            Check(result.Failure is null && result.Character!.State.Version == 1 && result.Character.OwnerId == player.Id, "create persists authenticated ownership and server revision");
            created = result.Character!;
        }
        await using (var db = context())
        {
            var c = await db.Characters.SingleAsync(c => c.Id == created.Id);
            Check(c.Name == "Player character" && JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson)!["double_score"].GetInt32() == 6, "normalized state and formula survive reload");
            var command = new CreateCharacter(new EfCharacterWriteStore(db), RpgPluginRegistry.Default);
            Check((await command.Execute(campaign.Id, spectator.Id, input)).Failure == CharacterWriteFailure.Forbidden, "spectator cannot insert character");
            Check((await command.Execute(campaign.Id, Guid.NewGuid(), input)).Failure == CharacterWriteFailure.NotFound, "non-member cannot insert character");
            Check(await db.Characters.CountAsync(c => c.CampaignId == campaign.Id) == 1, "denied creations produce no records");
        }
        await using (var db = context())
        {
            var values = new Dictionary<string, JsonElement>(created.State.Fields!) { ["gm_note"] = JsonSerializer.SerializeToElement("Preserve") };
            var result = await new SaveCharacter(new EfCharacterWriteStore(db), RpgPluginRegistry.Default).Execute(created.Id, gm.Id, created.State with { Fields = values }, requireRevision: true);
            Check(result.Failure is null, "GM edits player character"); created = result.Character!;
        }
        await using (var db = context())
        {
            var result = await new SaveCharacter(new EfCharacterWriteStore(db), RpgPluginRegistry.Default).Execute(created.Id, player.Id,
                created.State with { Fields = null, Health = 12 }, requireRevision: true);
            Check(result.Failure is null && result.Character!.State.Fields!["gm_note"].GetString() == "Preserve", "omitted fields preserve GM values");
            created = result.Character!;
        }
        await using (var a = context()) await using (var b = context())
        {
            var first = new EfCharacterWriteStore(a); var second = new EfCharacterWriteStore(b);
            var left = (await first.Load(created.Id, player.Id, default))!; var right = (await second.Load(created.Id, player.Id, default))!;
            var responses = await Task.WhenAll(first.Save(left, left.State with { Health = 8 }, player.Id, default),
                second.Save(right, right.State with { Health = 9 }, player.Id, default));
            Check(responses.Count(r => r.Failure is null) == 1 && responses.Count(r => r.Failure == CharacterWriteFailure.Conflict) == 1, "two concurrent saves have exactly one winner");
            created = responses.Single(r => r.Failure is null).Character!;
        }
        await using (var db = context())
        {
            Check(await db.CharacterRevisions.CountAsync(r => r.CharacterId == created.Id) == 3 && created.State.Version == 4, "one revision per accepted save");
            var payloads = await db.OutboxMessages.Select(e => e.Payload).ToListAsync();
            Check(payloads.Count(p => JsonSerializer.Deserialize<GameEvent>(p)!.AggregateId == created.Id) == 7,
                "one creation and two events per accepted save; no stale-write event");
            var read = await new EfCharacterWriteStore(db).Load(created.Id, Guid.NewGuid(), default);
            Check(read is null, "write adapter reader prevents IDOR");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterWriteStore(pending); var before = (await store.Load(created.Id, player.Id, default))!;
            await using (var revoke = context()) await revoke.CampaignMemberships.Where(m => m.CampaignId == campaign.Id && m.UserId == player.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "spectator"));
            Check((await store.Save(before, before.State with { Health = 2 }, player.Id, default)).Failure == CharacterWriteFailure.Forbidden, "commit rechecks permissions changed after load");
        }
        await using (var pending = context())
        {
            var store = new EfCharacterWriteStore(pending);
            var before = (await store.LoadCreationContext(campaign.Id, gm.Id, default))!;
            await using (var change = context())
            {
                var s = await change.RpgSystems.SingleAsync(s => s.Id == system.Id); s.SchemaJson = JsonSerializer.Serialize(schema with { Revision = 2 });
                await change.SaveChangesAsync();
            }
            Check((await store.Create(before, input, gm.Id, default)).Failure == CharacterWriteFailure.Conflict, "creation refuses a template changed after validation");
            Check(await pending.Characters.CountAsync(c => c.CampaignId == campaign.Id) == 1, "template conflict does not create a partial character");
        }
        var opSchema = new SystemDefinition([
            new("nex", "NEX", "number", "Agent", 5, 99, null),
            new("estado_regras", "State", "table", "Rules", null, null, null, Columns: [new("chave", "Key", "text"), new("valor", "Value", "number")])
        ], null, "ordem_paranormal", 7, "1.3");
        await using (var db = context())
        {
            var s = await db.RpgSystems.SingleAsync(s => s.Id == system.Id); s.SchemaJson = JsonSerializer.Serialize(opSchema);
            var encounter = new Encounter { CampaignId = campaign.Id, Title = "Overflow rollback", Status = "active" };
            db.Encounters.Add(encounter);
            db.EncounterParticipants.Add(new() { EncounterId = encounter.Id, CharacterId = created.Id,
                Health = created.State.Health, MaxHealth = created.State.MaxHealth, Conditions = new string('x', 995) });
            await db.SaveChangesAsync();
        }
        await using (var db = context())
        {
            var beforeEvents = await db.OutboxMessages.CountAsync();
            var fields = new Dictionary<string, JsonElement>(created.State.Fields!) { ["nex"] = JsonSerializer.SerializeToElement(35),
                ["estado_regras"] = JsonSerializer.SerializeToElement(new[] { new { chave = "cond_caido", valor = -1 } }) };
            var result = await new SaveCharacter(new EfCharacterWriteStore(db), RpgPluginRegistry.Default).Execute(created.Id, gm.Id,
                created.State with { Fields = fields, Health = 1 }, requireRevision: true);
            Check(result.Failure == CharacterWriteFailure.Invalid && !db.ChangeTracker.HasChanges(), "projection failure clears pending state and history");
            await using var verify = context();
            var c = await verify.Characters.SingleAsync(c => c.Id == created.Id);
            Check(c.Version == 4 && c.Health == created.State.Health && await verify.CharacterRevisions.CountAsync(r => r.CharacterId == created.Id) == 3,
                "projection failure leaves persisted character and history unchanged");
            Check(await verify.OutboxMessages.CountAsync() == beforeEvents, "projection failure publishes no events");
        }
        Console.WriteLine("Character PostgreSQL: creation/save, reload, formulas, GM values, concurrent writes, commit-time permission/template changes and encounter rollback passed.");
    }
}
