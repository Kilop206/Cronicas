using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Campaigns;
using Chronicles.Application.CharacterSheets;
using Chronicles.Application.RpgSystems;
using Chronicles.Application.Worlds;
using Chronicles.Domain;
using Chronicles.Domain.Events;
using Chronicles.Infrastructure.Campaigns;
using Chronicles.Infrastructure.Characters;
using Chronicles.Infrastructure.RpgSystems;
using Chronicles.Infrastructure.Worlds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

public static class CampaignAssociationPersistenceTests
{
    public static async Task Run(Func<AppDbContext> context)
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Association PostgreSQL: " + label); }
        static EfCampaignAssociationStore Store(AppDbContext db) => new(db, new EfRpgSystemCampaignAccess(db), new EfWorldCampaignAccess(db));
        static async Task<GameEvent[]> Events(AppDbContext db) => (await db.OutboxMessages.AsNoTracking().Select(e => e.Payload).ToArrayAsync()).Select(s => JsonSerializer.Deserialize<GameEvent>(s)!).ToArray();
        var owner = new User { Email = "association-owner@example.test", DisplayName = "Owner", PasswordHash = "fixture" };
        var player = new User { Email = "association-player@example.test", DisplayName = "Player", PasswordHash = "fixture" };
        var foreign = new User { Email = "association-foreign@example.test", DisplayName = "Other owner", PasswordHash = "fixture" };
        var schema = JsonSerializer.Serialize(new SystemDefinition([new("score", "Original", "number", "Stats", 0, 20, null)], null));
        var first = new RpgSystem { Name = "Original association", OwnerId = owner.Id, SchemaJson = schema };
        var second = new RpgSystem { Name = "Second association", OwnerId = owner.Id, SchemaJson = schema };
        var third = new RpgSystem { Name = "Third association", OwnerId = owner.Id, SchemaJson = schema };
        var unavailable = new RpgSystem { Name = "Foreign system", OwnerId = foreign.Id, SchemaJson = schema };
        var world = new World { Name = "Owned world", OwnerId = owner.Id };
        var nextWorld = new World { Name = "Next owned world", OwnerId = owner.Id };
        var privateWorld = new World { Name = "Foreign world", OwnerId = foreign.Id };
        var campaign = new Campaign { Name = "Association fixture", OwnerId = owner.Id, SystemId = first.Id };
        var pinned = new Character { Name = "Pinned association", CampaignId = campaign.Id, OwnerId = player.Id, FieldsJson = "{\"score\":2}" };
        var dynamic = new Character { Name = "Unbound association", CampaignId = campaign.Id, OwnerId = player.Id, FieldsJson = pinned.FieldsJson };
        Guid binding;
        await using (var db = context())
        {
            db.Users.AddRange(owner, player, foreign); db.RpgSystems.AddRange(first, second, third, unavailable); db.Worlds.AddRange(world, nextWorld, privateWorld);
            db.Campaigns.Add(campaign); db.Characters.AddRange(pinned, dynamic);
            db.CampaignMemberships.AddRange(new() { CampaignId = campaign.Id, UserId = owner.Id, Role = "owner" }, new() { CampaignId = campaign.Id, UserId = player.Id, Role = "co-gm" });
            await db.SaveChangesAsync();
            binding = (await new PinCharacterSystem(new EfCharacterSystemStore(db)).Execute(pinned.Id, owner.Id, 1, first.Id, 1)).Character!.SystemVersionId!.Value;
        }
        async Task<CampaignResult<bool>> Change(CampaignAssociationKind kind, Guid? target, CampaignContext? before = null)
        {
            await using var db = context(); var store = Store(db);
            return await store.Change(before ?? (await store.Read(campaign.Id, owner.Id, default))!, owner.Id, new(kind, target), default);
        }
        await using (var db = context())
        {
            var commands = new ManageCampaignAssociations(Store(db));
            Check((await commands.System(campaign.Id, player.Id, second.Id)).Failure == CampaignFailure.NotFound, "co-GM cannot associate assets");
            Check((await commands.System(campaign.Id, owner.Id, unavailable.Id)).Errors?.ContainsKey("systemId") == true, "foreign systems stay unavailable");
            Check((await commands.World(campaign.Id, owner.Id, privateWorld.Id)).Errors?.ContainsKey("worldId") == true, "foreign worlds stay unavailable");
            try { await new EfRpgSystemCampaignAccess(db).HoldOwnedReference(first.Id, owner.Id, default); throw new Exception("Expected transaction guard."); }
            catch (InvalidOperationException e) when (e.Message.Contains("active local transaction")) { }
            try { await new EfWorldCampaignAccess(db).HoldOwnedReference(world.Id, owner.Id, default); throw new Exception("Expected transaction guard."); }
            catch (InvalidOperationException e) when (e.Message.Contains("active local transaction")) { }
        }
        Check((await Change(CampaignAssociationKind.System, second.Id)).Value && (await Change(CampaignAssociationKind.World, world.Id)).Value, "owner can associate both modules");
        await using (var db = context())
        {
            var effective = new EfCharacterSystemStore(db);
            Check((await effective.ReadSystem(pinned.Id, player.Id, default)).System!.SystemVersionId == binding &&
                (await effective.ReadSystem(pinned.Id, player.Id, default)).System!.Id == first.Id &&
                (await effective.ReadSystem(dynamic.Id, player.Id, default)).System!.Id == second.Id, "pinned and unbound sheets keep different effective sources");
            var snapshots = await db.Characters.AsNoTracking().Where(c => c.CampaignId == campaign.Id).ToArrayAsync();
            Check(snapshots.Single(c => c.Id == pinned.Id).Version == 2 && snapshots.Single(c => c.Id == dynamic.Id).Version == 1 &&
                snapshots.All(c => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson)!["score"].GetInt32() == 2), "association never rewrites character state/revisions");
            var before = (await Store(db).Read(campaign.Id, owner.Id, default))!; var count = await db.OutboxMessages.CountAsync();
            Check(!(await Change(CampaignAssociationKind.System, second.Id)).Value && (await Store(db).Read(campaign.Id, owner.Id, default))!.Document.UpdatedAt == before.Document.UpdatedAt &&
                await db.OutboxMessages.CountAsync() == count, "no-op has no timestamp or event side effects");
            var events = (await Events(db)).Where(e => e.CampaignId == campaign.Id).ToArray();
            Check(events.Count(e => e.Type == GameEventTypes.CampaignSystemChanged) == 1 && events.Count(e => e.Type == GameEventTypes.CampaignWorldChanged) == 1 &&
                events.Where(e => e.Type == GameEventTypes.CampaignSystemChanged).All(e => e.Revision is null && e.AggregateId == campaign.Id), "durable association events contain metadata only");
        }
        Check((await Change(CampaignAssociationKind.System, first.Id)).Value && (await Change(CampaignAssociationKind.World, null)).Value, "detach is explicit");
        CampaignContext initial;
        await using (var db = context()) initial = (await Store(db).Read(campaign.Id, owner.Id, default))!;
        var race = await Task.WhenAll(Change(CampaignAssociationKind.System, second.Id, initial), Change(CampaignAssociationKind.System, third.Id, initial));
        Check(race.Count(r => r.Failure is null) == 1 && race.Count(r => r.Failure == CampaignFailure.Conflict) == 1, "same-link stale decisions cannot overwrite each other");
        await Change(CampaignAssociationKind.System, first.Id);
        await using (var db = context()) initial = (await Store(db).Read(campaign.Id, owner.Id, default))!;
        var independent = await Task.WhenAll(Change(CampaignAssociationKind.System, second.Id, initial), Change(CampaignAssociationKind.World, world.Id, initial));
        Check(independent.All(r => r.Failure is null && r.Value), "independent system/world writes preserve both references");
        await using (var db = context())
        {
            var before = (await Store(db).Read(campaign.Id, owner.Id, default))!; var count = await db.OutboxMessages.CountAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options;
            try { await using var fail = new AppDbContext(options); await Store(fail).Change(before, owner.Id, new(CampaignAssociationKind.System, third.Id), default); throw new Exception("Expected rollback."); }
            catch (InvalidOperationException e) when (e.Message == "Forced association failure") { }
            Check((await Store(db).Read(campaign.Id, owner.Id, default)) == before && await db.OutboxMessages.CountAsync() == count,
                "post-save failure rolls back association, timestamp and outbox");
        }
        // Prove both public adapters retain row locks until the outer campaign transaction commits.
        foreach (var kind in new[] { CampaignAssociationKind.System, CampaignAssociationKind.World })
        {
            var gate = new Gate(); await using var changing = context();
            var guarded = new EfCampaignAssociationStore(changing, new GatedSystem(new EfRpgSystemCampaignAccess(changing), gate, kind == CampaignAssociationKind.System),
                new GatedWorld(new EfWorldCampaignAccess(changing), gate, kind == CampaignAssociationKind.World));
            var before = (await guarded.Read(campaign.Id, owner.Id, default))!;
            var operation = guarded.Change(before, owner.Id, new(kind, kind == CampaignAssociationKind.System ? third.Id : nextWorld.Id), default);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                await using var probe = context(); await using var tx = await probe.Database.BeginTransactionAsync();
                await probe.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '150ms'");
                try
                {
                    if (kind == CampaignAssociationKind.System) await probe.RpgSystems.FromSqlInterpolated($"SELECT * FROM rpg_systems WHERE \"Id\" = {third.Id} FOR UPDATE").AsNoTracking().ToListAsync();
                    else await probe.Worlds.FromSqlInterpolated($"SELECT * FROM worlds WHERE \"Id\" = {nextWorld.Id} FOR UPDATE").AsNoTracking().ToListAsync();
                    throw new Exception("Target deletion lock incorrectly acquired.");
                }
                catch (Exception e) when (e is PostgresException { SqlState: "55P03" } ||
                    e is InvalidOperationException { InnerException: PostgresException { SqlState: "55P03" } }) { }
            }
            finally { gate.Release.TrySetResult(); }
            Check((await operation).Failure is null, "target reference remains protected until commit");
        }
        await using (var db = context())
        {
            var before = (await Store(db).Read(campaign.Id, owner.Id, default))!;
            await using (var revoke = context()) { (await revoke.CampaignMemberships.SingleAsync(m => m.CampaignId == campaign.Id && m.UserId == owner.Id)).Role = "player"; await revoke.SaveChangesAsync(); }
            Check((await Store(db).Change(before, owner.Id, new(CampaignAssociationKind.System, null), default)).Failure == CampaignFailure.NotFound, "authority is rechecked at commit");
        }
        Console.WriteLine("Campaign associations PostgreSQL: ownership, module leases, pinned/state preservation, no-op, same-link conflicts, independent links, atomic events/rollback and commit-time revocation passed.");
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    { public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default) => throw new InvalidOperationException("Forced association failure"); }
    private sealed class Gate
    { public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private sealed class GatedSystem(IRpgSystemCampaignAccess inner, Gate gate, bool enabled) : IRpgSystemCampaignAccess
    {
        public async Task<bool> HoldOwnedReference(Guid id, Guid actor, CancellationToken ct)
        { var result = await inner.HoldOwnedReference(id, actor, ct); if (enabled) { gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct); } return result; }
    }
    private sealed class GatedWorld(IWorldCampaignAccess inner, Gate gate, bool enabled) : IWorldCampaignAccess
    {
        public async Task<bool> HoldOwnedReference(Guid id, Guid actor, CancellationToken ct)
        { var result = await inner.HoldOwnedReference(id, actor, ct); if (enabled) { gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct); } return result; }
    }
}
