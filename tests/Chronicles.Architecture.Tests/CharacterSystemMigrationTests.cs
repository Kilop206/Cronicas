using System.Text.Json;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;

public static class CharacterSystemMigrationTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Assisted migration: " + label); }
        static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
        var source = new SystemDefinition([new("old_score", "Score", "number", "Stats", 0, 20, null)], null);
        var target = source with { Revision = 2, Fields = [source.Fields[0] with { Key = "new_score" },
            new("derived", "Derived", "number", "Stats", 0, 100, null, Formula: "new_score * 3"),
            new("new_flag", "Flag", "boolean", "Stats", null, null, null, DefaultValue: Json(true))] };
        Dictionary<string, JsonElement> values = new() { ["old_score"] = Json(4), ["legacy_notes"] = Json("keep me") };
        var plan = CharacterSystemMigration.Plan(source, target, values, new() { ["old_score"] = "new_score" }, null, RpgPluginRegistry.Default);
        Check(plan.Errors.Count == 0 && plan.Fields["new_score"].GetInt32() == 4 && plan.Fields["derived"].GetInt32() == 12, "rename is copied and formulas recalculated");
        Check(plan.Fields["new_flag"].GetBoolean() && plan.PreservedKeys.Contains("old_score") && plan.Fields["legacy_notes"].GetString() == "keep me", "defaults and retired values preserved");
        Check(values.Count == 2 && !values.ContainsKey("derived"), "input is not mutated");
        Check(CharacterSystemMigration.Plan(source, target, values, new() { ["old_score"] = "derived" }, null, RpgPluginRegistry.Default).Errors.Count > 0, "cannot map formula field");
        Check(CharacterSystemMigration.Plan(source, target, values, new() { ["old_score"] = "new_score", ["legacy_notes"] = "new_score" }, null, RpgPluginRegistry.Default).Errors.Count > 0, "duplicate destination blocked");
        Check(CharacterSystemMigration.Plan(source, target, values, new() { ["old_score"] = "new_score" }, new() { ["new_score"] = Json(1) }, RpgPluginRegistry.Default).Errors.Count > 0, "ambiguous adjustment/mapping blocked");
        Check(CharacterSystemMigration.Plan(source, target, new(values) { ["new_score"] = Json(8) }, new() { ["old_score"] = "new_score" }, null, RpgPluginRegistry.Default).Errors.Count > 0, "occupied destination not overwritten");
        var changedType = source with { Revision = 2, Fields = [source.Fields[0] with { Type = "select", Options = ["low", "high"] }] };
        Check(CharacterSystemMigration.Plan(source, changedType, values, null, null, RpgPluginRegistry.Default).Errors.ContainsKey("value:old_score"), "incompatible types need explicit correction");
        Check(CharacterSystemMigration.Plan(source, changedType, values, null, new() { ["old_score"] = Json("high") }, RpgPluginRegistry.Default).Errors.Count == 0, "valid explicit correction accepted");
        Check(CharacterSystemMigration.Plan(source, target with { RulesetVersion = "new-edition" }, values, null, null, RpgPluginRegistry.Default).Errors.ContainsKey("edition"), "edition conversions not guessed");
        Check(CharacterSystemMigration.Plan(source, target, values, null, new() { ["legacy_notes"] = Json("changed") }, RpgPluginRegistry.Default).Errors.Count > 0, "unknown preserved data cannot be edited via migration");
        var retiredTable = Json(new[] { new { name = "Keep this row" } });
        var emptyTarget = source with { Revision = 2, Fields = [] };
        Dictionary<string, JsonElement> archivedValues = new() { ["old_table"] = retiredTable };
        Check(CharacterSystemMigration.Plan(source, emptyTarget, archivedValues, null, null, RpgPluginRegistry.Default).Errors.Count == 0,
            "retiring the last declared field preserves structured data");
        Check(CharacterSheetValidation.ValidateFields(emptyTarget, new(archivedValues), archivedValues) is null &&
            CharacterSheetValidation.ValidateFields(emptyTarget, new(archivedValues)) is not null,
            "normal saves preserve archived tables but cannot introduce arbitrary new free tables");
        var op = new SystemDefinition([new("estado_regras", "Rules", "table", "Rules", null, null, null,
            Columns: [new("chave", "Key", "text"), new("valor", "Value", "number")])], null, "ordem_paranormal", 7, "1.3");
        Check(CharacterSystemMigration.Plan(op, op with { TemplateVersion = 999 }, [], null, null, RpgPluginRegistry.Default).Errors.ContainsKey("plugin"), "unsupported known schema rejected");
        Check(CharacterSystemMigration.Plan(op, op with { Fields = [] }, [], null, null, RpgPluginRegistry.Default).Errors.ContainsKey("plugin"), "migration cannot silently disable existing executor");
        var tiny = target with { Fields = [new("derived", "Limited", "number", "Stats", 0, 1, null, Formula: "2")] };
        Check(CharacterSystemMigration.Plan(source, tiny, values, null, null, RpgPluginRegistry.Default).Errors.Count > 0, "formula bounds block migration");
        var actor = Guid.NewGuid();
        var snapshot = new CharacterSheetSnapshot(Guid.NewGuid(), Guid.NewGuid(), actor, "owner",
            new("Character", null, null, 4, 10, 3, 10, 2, 10, values, 2), source, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var frozen = FrozenSystemTemplate.Capture("New model", "", target);
        var destination = new SystemMigrationDestination(new(null, "New model", 2, null, 1, null, frozen.Fingerprint(), target.Fields, true), frozen);
        var input = new SystemMigrationInput(2, snapshot.SystemVersionId!.Value, null, 2, frozen.Fingerprint(), new() { ["old_score"] = "new_score" });
        var store = new MemoryStore(snapshot, destination); var service = new MigrateCharacterSystem(store, RpgPluginRegistry.Default);
        var preview = (await service.Preview(snapshot.Id, actor, input)).Preview!;
        Check(preview.CanApply && store.Commits == 0, "preview is read-only");
        Check((await service.Apply(snapshot.Id, actor, input, "forged")).Failure == CharacterWriteFailure.Conflict && store.Commits == 0, "forged preview rejected");
        Check((await service.Preview(snapshot.Id, actor, input with { ExpectedVersion = 1 })).Failure == CharacterWriteFailure.Conflict, "stale character rejected");
        Check((await service.Preview(snapshot.Id, actor, input with { TargetHash = "stale" })).Failure == CharacterWriteFailure.Conflict, "stale target rejected");
        Check((await service.Apply(snapshot.Id, actor, input with { Values = new() { ["new_flag"] = Json(false) } }, preview.PreviewHash!)).Failure == CharacterWriteFailure.Conflict, "changed plan requires fresh review");
        var canonicalA = MigrateCharacterSystem.Review(snapshot, actor, destination, input with { Values = new() { ["new_flag"] = Json(false), ["new_score"] = Json(3) }, Mappings = [] }, RpgPluginRegistry.Default).Preview!;
        var canonicalB = MigrateCharacterSystem.Review(snapshot, actor, destination, input with { Values = new() { ["new_score"] = Json(3), ["new_flag"] = Json(false) }, Mappings = [] }, RpgPluginRegistry.Default).Preview!;
        Check(canonicalA.CanApply && canonicalA.PreviewHash == canonicalB.PreviewHash, "dictionary insertion order does not invalidate the reviewed plan");
        Check((await service.Apply(snapshot.Id, Guid.NewGuid(), input, preview.PreviewHash!)).Failure == CharacterWriteFailure.Conflict, "digest bound to actor");
        Check((await service.Apply(snapshot.Id, actor, input, preview.PreviewHash!)).Failure is null && store.Commits == 1, "reviewed plan delegates atomic commit");
        store.Current = snapshot with { Role = "player" };
        Check((await service.Targets(snapshot.Id, actor)).Failure == CharacterWriteFailure.Forbidden, "player cannot enumerate target schemas");
        Check((await service.Apply(snapshot.Id, actor, input, preview.PreviewHash!)).Failure == CharacterWriteFailure.Forbidden && store.Commits == 1, "player cannot apply reviewed master plan");
        store.Current = snapshot with { SystemVersionId = null };
        Check((await service.Preview(snapshot.Id, actor, input)).Failure == CharacterWriteFailure.Invalid, "unbound characters must pin first");
        Console.WriteLine("Assisted migration domain/application: mappings, collision/type/edition checks, preserved values, safe formulas, review digest, stale plans and master authorization passed.");
    }

    private sealed class MemoryStore(CharacterSheetSnapshot current, SystemMigrationDestination target) : ICharacterSystemMigrationStore
    {
        public CharacterSheetSnapshot Current = current; public int Commits;
        public Task<CharacterSheetSnapshot?> Load(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<CharacterSheetSnapshot?>(Current);
        public Task<SystemMigrationTarget[]> Targets(CharacterSheetSnapshot before, Guid actor, CancellationToken ct) => Task.FromResult(new[] { target.Summary });
        public Task<SystemMigrationAudit[]> History(CharacterSheetSnapshot before, CancellationToken ct) => Task.FromResult(Array.Empty<SystemMigrationAudit>());
        public Task<SystemMigrationDestination?> Destination(CharacterSheetSnapshot before, Guid actor, Guid? version, CancellationToken ct) => Task.FromResult<SystemMigrationDestination?>(target);
        public Task<CharacterWriteResponse> Commit(CharacterSheetSnapshot before, Guid actor, SystemMigrationInput input, string previewHash, CancellationToken ct)
        { Commits++; return Task.FromResult(new CharacterWriteResponse(before)); }
    }
}
