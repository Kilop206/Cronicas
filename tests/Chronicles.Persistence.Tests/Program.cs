using System.Collections.Concurrent;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Application.Events;
using Chronicles.Domain;
using Chronicles.Domain.Events;
using Chronicles.Infrastructure.Characters;
using Chronicles.Infrastructure.Messaging;
using Chronicles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
var configured = Environment.GetEnvironmentVariable("ARCHITECTURE_TEST_DB")
    ?? throw new InvalidOperationException("Set ARCHITECTURE_TEST_DB to a loopback PostgreSQL admin connection. Tests create their own database.");
var adminSettings = new NpgsqlConnectionStringBuilder(configured);
Check(adminSettings.Host is "127.0.0.1" or "localhost" && adminSettings.Database == "postgres", "Only loopback postgres admin database is allowed.");
var database = "cronicas_architecture_" + Guid.NewGuid().ToString("N");
await using var admin = new NpgsqlConnection(adminSettings.ConnectionString);
await admin.OpenAsync();
await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
var settings = new NpgsqlConnectionStringBuilder(configured) { Database = database, Pooling = false };
AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(settings.ConnectionString).Options);
const string previousMigration = "20261104120000_NpcThreatSheets";
const string outboxMigration = "20261104172257_IntegrationOutbox";
try
{
    await using var db = Context();
    var migrator = db.GetService<IMigrator>();
    Check(!db.Database.HasPendingModelChanges(), "EF snapshot must exactly match the runtime model.");
    await migrator.MigrateAsync(previousMigration);
    try { await DatabaseStartup.EnsureReady(db, false); throw new Exception("Production startup applied/accepted pending migrations."); }
    catch (InvalidOperationException error) when (error.Message.StartsWith("Database migrations are pending.", StringComparison.Ordinal)) { }
    Check((await db.Database.GetAppliedMigrationsAsync()).Last() == previousMigration, "Production guard never applies schema changes.");
    var sentinel = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users (\"Id\",\"Email\",\"PasswordHash\",\"DisplayName\",\"CreatedAt\",\"Accent\") VALUES ({sentinel},'migration@example.test','fixture','Migration sentinel',now(),'gold')");
    await db.Database.MigrateAsync();
    await DatabaseStartup.EnsureReady(db, false);
    Check(await db.Users.AnyAsync(u => u.Id == sentinel), "Upgrade preserves existing users.");
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "All migrations applied.");
    var latest = (await db.Database.GetAppliedMigrationsAsync()).Last();
    var sql = migrator.GenerateScript(previousMigration, latest, MigrationsSqlGenerationOptions.Idempotent);
    Check(sql.Contains("CREATE TABLE integration_outbox") && sql.Contains("CREATE TABLE rpg_system_versions") &&
        !sql.Contains("DROP TABLE") && !sql.Contains("DELETE FROM") && !sql.Contains("UPDATE characters"), "Release migrations must only add schema without backfilling character data.");
    var migrationCount = (await db.Database.GetAppliedMigrationsAsync()).Count();
    await db.Database.ExecuteSqlRawAsync(sql);
    await db.Database.ExecuteSqlRawAsync(sql);
    Check((await db.Database.GetAppliedMigrationsAsync()).Count() == migrationCount && await db.Users.AnyAsync(u => u.Id == sentinel),
        "Reviewed idempotent SQL can be reapplied without duplicating migrations or changing existing data.");
    var root = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "compose.yaml"))) root = root.Parent;
    var output = Path.Combine(root!.FullName, "docs/architecture/releases"); Directory.CreateDirectory(output);
    await File.WriteAllTextAsync(Path.Combine(output, "integration-outbox-up.sql"), migrator.GenerateScript(previousMigration, outboxMigration, MigrationsSqlGenerationOptions.Idempotent).TrimEnd() + "\n");
    await File.WriteAllTextAsync(Path.Combine(output, "integration-outbox-down.sql"), migrator.GenerateScript(outboxMigration, previousMigration).TrimEnd() + "\n");
    await File.WriteAllTextAsync(Path.Combine(output, "character-system-versions-up.sql"), migrator.GenerateScript(outboxMigration, latest, MigrationsSqlGenerationOptions.Idempotent).TrimEnd() + "\n");
    await File.WriteAllTextAsync(Path.Combine(output, "character-system-versions-down.sql"), migrator.GenerateScript(latest, outboxMigration).TrimEnd() + "\n");

    var user = new User { Email = "owner@example.test", PasswordHash = "fixture", DisplayName = "Owner" };
    var system = new RpgSystem { OwnerId = user.Id, Name = "Renamed campaign system", SchemaJson = JsonSerializer.Serialize(new SystemDefinition([
        new("nex", "NEX", "number", "Agent", 5, 99, null),
        new("estado_regras", "State", "table", "Rules", null, null, null, Columns: [new("chave", "Key", "text"), new("valor", "Value", "number")])
    ], null, "ordem_paranormal", 7, "1.3")) };
    var campaign = new Campaign { OwnerId = user.Id, Name = "Persistence fixture", SystemId = system.Id };
    var character = new Character { CampaignId = campaign.Id, OwnerId = user.Id, Name = "Agent", Health = 20, MaxHealth = 30,
        FieldsJson = "{\"nex\":35}" };
    db.Users.Add(user); db.RpgSystems.Add(system); db.Campaigns.Add(campaign); db.Characters.Add(character);
    db.CampaignMemberships.Add(new() { CampaignId = campaign.Id, UserId = user.Id, Role = "owner" });
    await db.SaveChangesAsync();
    var staged = await db.OutboxMessages.CountAsync();
    Check(staged == 4, "Creation stages CharacterCreated, CampaignCreated, PlayerJoinedCampaign and RPGSystemRegistered atomically.");
    var payloads = await db.OutboxMessages.Select(e => e.Payload).ToArrayAsync();
    Check(payloads.All(p => !p.Contains("example.test") && !p.Contains("PasswordHash") && !p.Contains("FieldsJson")), "Events contain metadata only.");
    await using (var rollback = Context())
    {
        await using var transaction = await rollback.Database.BeginTransactionAsync();
        rollback.Campaigns.Add(new() { Name = "Must roll back", OwnerId = user.Id });
        await rollback.SaveChangesAsync();
        await transaction.RollbackAsync();
    }
    Check(await db.OutboxMessages.CountAsync() == staged && !await db.Campaigns.AnyAsync(c => c.Name == "Must roll back"), "No business write or event escapes a rollback.");

    await using var first = Context(); await using var second = Context();
    var firstStore = new EfCharacterRuleStore(first); var secondStore = new EfCharacterRuleStore(second);
    var left = (await firstStore.Load(character.Id, user.Id, default))!;
    var right = (await secondStore.Load(character.Id, user.Id, default))!;
    var result = SheetRuleActions.Apply(left.System!, left.State, new("damage", 3), _ => 10);
    Check(await firstStore.Commit(new(left, result, user.Id, "private"), default) is not null, "First version commits.");
    Check(await secondStore.Commit(new(right, result, user.Id, "private"), default) is null, "Second stale version conflicts.");
    db.ChangeTracker.Clear();
    var actual = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
    Check(actual.Health == 17 && actual.Version == 2 && await db.CharacterRevisions.CountAsync() == 1, "Resources/history/revision commit exactly once.");
    Check(await db.OutboxMessages.CountAsync() == staged + 2, "Only the successful edit stages CharacterUpdated and CharacterSheetUpdated.");
    Check(await firstStore.Load(character.Id, Guid.NewGuid(), default) is null, "IDOR denied by the persistence adapter.");

    var collector = new CollectingHandler();
    await using (var dispatch = Context())
        Check(await new OutboxDispatcher(dispatch, [collector]).Dispatch() == staged + 2, "Committed events are delivered.");
    await using (var again = Context())
        Check(await new OutboxDispatcher(again, [collector]).Dispatch() == 0, "Processed events are not delivered again.");

    var retryEvent = new GameEvent(Guid.NewGuid(), GameEventTypes.CharacterSheetUpdated, 1, DateTime.UtcNow, character.Id, campaign.Id, 2, null);
    db.OutboxMessages.Add(new() { Id = retryEvent.EventId, Payload = JsonSerializer.Serialize(retryEvent), OccurredAt = retryEvent.OccurredAt, AvailableAt = retryEvent.OccurredAt });
    await db.SaveChangesAsync();
    var retryHandler = new RetryHandler();
    await using (var dispatch = Context()) Check(await new OutboxDispatcher(dispatch, [retryHandler]).Dispatch(1) == 0, "Failure remains pending.");
    var retry = await db.OutboxMessages.AsNoTracking().SingleAsync(e => e.Id == retryEvent.EventId);
    Check(retry.Attempts == 1 && retry.ProcessedAt is null && retry.AvailableAt > DateTime.UtcNow, "Retries record bounded backoff.");
    await db.OutboxMessages.Where(e => e.Id == retryEvent.EventId).ExecuteUpdateAsync(s => s.SetProperty(e => e.AvailableAt, DateTime.UtcNow.AddMinutes(-1)));
    await using (var dispatch = Context()) Check(await new OutboxDispatcher(dispatch, [retryHandler]).Dispatch(1) == 1, "Failed event can be retried.");
    Check(retryHandler.Attempts == 2 && retryHandler.Effects.Count == 1, "Consumer idempotency is keyed by stable EventId.");

    var block = new BlockingHandler();
    var now = DateTime.UtcNow;
    foreach (var id in new[] { Guid.NewGuid(), Guid.NewGuid() }) db.OutboxMessages.Add(new() { Id = id, OccurredAt = now,
        AvailableAt = now, Payload = JsonSerializer.Serialize(retryEvent with { EventId = id }) });
    await db.SaveChangesAsync();
    await using (var a = Context()) await using (var b = Context())
    {
        var delivery = new OutboxDispatcher(a, [block]).Dispatch(1);
        await block.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check(await new OutboxDispatcher(b, [collector]).Dispatch(1) == 1, "Other worker skips a locked claim.");
        block.Release.SetResult();
        Check(await delivery == 1, "Claim completes without duplicate processing.");
    }
    Check(await db.OutboxMessages.AllAsync(e => e.ProcessedAt != null), "No pending events before technical rollback.");
    await migrator.MigrateAsync(previousMigration);
    Check(await db.Users.AnyAsync(u => u.Id == sentinel) && await db.Characters.AnyAsync(c => c.Id == character.Id && c.Health == 17), "Rollback preserves all game data.");
    await db.Database.MigrateAsync();
    Check(await db.Characters.AnyAsync(c => c.Id == character.Id && c.Version == 2), "Re-upgrade preserves revision and state.");
    await CharacterWritePersistenceTests.Run(Context);
    await CharacterLifecyclePersistenceTests.Run(Context);
    await CharacterAssetPersistenceTests.Run(Context);
    await CharacterCompanionPersistenceTests.Run(Context);
    await CharacterSystemPersistenceTests.Run(Context);
    Console.WriteLine("PostgreSQL: full migration chain, upgrade/rollback/re-upgrade, preserved data, exact EF snapshot, atomic events/history, stale writes, IDOR, retries/idempotency and concurrent claims passed.");
}
finally
{
    // This exact randomly generated database was created above; no configured database is ever dropped.
    await using var cleanup = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
    await cleanup.ExecuteNonQueryAsync();
}

sealed class CollectingHandler : IGameEventHandler
{
    public ConcurrentDictionary<Guid, bool> Events = new();
    public Task Handle(GameEvent message, CancellationToken ct) { Events.TryAdd(message.EventId, true); return Task.CompletedTask; }
}
sealed class RetryHandler : IGameEventHandler
{
    public int Attempts;
    public HashSet<Guid> Effects = [];
    public Task Handle(GameEvent message, CancellationToken ct)
    {
        Effects.Add(message.EventId);
        if (++Attempts == 1) throw new InvalidOperationException("Simulated delivery failure after a consumer effect.");
        return Task.CompletedTask;
    }
}
sealed class BlockingHandler : IGameEventHandler
{
    public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task Handle(GameEvent message, CancellationToken ct) { Entered.SetResult(); await Release.Task.WaitAsync(ct); }
}
