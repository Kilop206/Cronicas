using System.Text.Json;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;

public static class CharacterWriteTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Character writes: " + label); }
        var actor = Guid.NewGuid();
        var schema = new SystemDefinition([
            new("base", "Base", "number", "Stats", 0, 20, null),
            new("dobro", "Double", "number", "Stats", 0, 40, null, Formula: "base * 2"),
            new("segredo", "Master field", "text", "Notes", null, null, null, EditRole: "gm")
        ], null);
        var context = new CharacterCreationContext(Guid.NewGuid(), "player", Guid.NewGuid(), schema);
        var store = new MemoryWriter(actor, context);
        var create = new CreateCharacter(store, RpgPluginRegistry.Default);
        var fields = new Dictionary<string, JsonElement> { ["base"] = JsonSerializer.SerializeToElement(2) };
        var input = new CharacterSheetState("  Character  ", "  ", null, 10, 20, 10, 20, 10, 20, fields, 999);
        var created = await create.Execute(context.CampaignId, actor, input);
        Check(created.Failure is null && created.Character!.OwnerId == actor && created.Character.State.Version == 1, "ownership and initial revision come from server");
        Check(created.Character!.State.Name == "Character" && created.Character.State.Fields!["dobro"].GetInt32() == 4, "normalization and formulas run before persistence");
        Check(!fields.ContainsKey("dobro") && input.Name.StartsWith(' '), "command does not mutate caller's draft");
        Check((await create.Execute(context.CampaignId, Guid.NewGuid(), input)).Failure == CharacterWriteFailure.NotFound, "inaccessible campaign is hidden");
        store.Context = context with { Role = "spectator" };
        Check((await create.Execute(context.CampaignId, actor, input)).Failure == CharacterWriteFailure.Forbidden, "spectator cannot create");
        store.Context = context;
        Check((await create.Execute(context.CampaignId, actor, input with { MaxHealth = 10000 })).Failure == CharacterWriteFailure.Invalid, "invalid resources do not reach persistence");
        var restricted = new Dictionary<string, JsonElement>(fields) { ["segredo"] = JsonSerializer.SerializeToElement("forged") };
        Check((await create.Execute(context.CampaignId, actor, input with { Fields = restricted })).Failure == CharacterWriteFailure.Invalid, "player cannot set GM field at creation");
        store.Context = context with { Role = "gm" };
        Check((await create.Execute(context.CampaignId, actor, input with { Fields = restricted })).Failure is null, "GM can initialize authorized fields");

        var historical = JsonSerializer.SerializeToElement(new { retained = new[] { 1, 2 } });
        var previousFields = new Dictionary<string, JsonElement> { ["base"] = JsonSerializer.SerializeToElement(2),
            ["dobro"] = JsonSerializer.SerializeToElement(4), ["segredo"] = JsonSerializer.SerializeToElement("unchanged"), ["retired_field"] = historical };
        store.Current = new(Guid.NewGuid(), context.CampaignId, actor, "player", input with { Name = "Character", Fields = previousFields, Version = 5 }, schema, DateTime.UtcNow, context.SystemId);
        var save = new SaveCharacter(store, RpgPluginRegistry.Default);
        var omitted = await save.Execute(store.Current.Id, actor, input with { Fields = null, Version = 5 }, requireRevision: true);
        Check(omitted.Failure is null && JsonElement.DeepEquals(omitted.Character!.State.Fields!["retired_field"], historical), "omitted fields retain old structured data");
        Check(previousFields["dobro"].GetInt32() == 4 && !ReferenceEquals(previousFields, omitted.Character!.State.Fields), "snapshot fields are not reused for mutation");
        var current = store.Current;
        var counts = store.Writes;
        Check((await save.Execute(current.Id, actor, input with { Fields = null, Version = 5 })).Failure == CharacterWriteFailure.Conflict, "stale revision rejected");
        Check((await save.Execute(current.Id, actor, input with { Fields = null, Version = null }, requireRevision: true)).Failure == CharacterWriteFailure.Invalid, "v1 requires revision");
        Check(store.Writes == counts, "invalid revisions never invoke write port");
        var changed = new Dictionary<string, JsonElement>(current.State.Fields!) { ["base"] = JsonSerializer.SerializeToElement(3), ["dobro"] = JsonSerializer.SerializeToElement(999) };
        var updated = await save.Execute(current.Id, actor, input with { Fields = changed, Version = current.State.Version }, requireRevision: true);
        Check(updated.Failure is null && updated.Character!.State.Fields!["dobro"].GetInt32() == 6 && changed["dobro"].GetInt32() == 999, "derived values replace forged values in an independent copy");
        current = store.Current;
        var forgedGm = new Dictionary<string, JsonElement>(current.State.Fields!) { ["segredo"] = JsonSerializer.SerializeToElement("changed") };
        Check((await save.Execute(current.Id, actor, input with { Fields = forgedGm, Version = current.State.Version })).Failure == CharacterWriteFailure.Invalid, "GM field restriction applies to ordinary save");
        var forgedHistory = new Dictionary<string, JsonElement>(current.State.Fields!) { ["retired_field"] = JsonSerializer.SerializeToElement("changed") };
        Check((await save.Execute(current.Id, actor, input with { Fields = forgedHistory, Version = current.State.Version })).Failure == CharacterWriteFailure.Invalid, "unknown legacy field can be retained but not forged");
        store.Current = current with { OwnerId = Guid.NewGuid() };
        Check((await save.Execute(current.Id, actor, input)).Failure == CharacterWriteFailure.Forbidden, "campaign member cannot edit someone else's character");
        store.Current = current with { Role = "spectator" };
        Check((await save.Execute(current.Id, actor, input)).Failure == CharacterWriteFailure.Forbidden, "spectator cannot edit own character");
        store.Current = current;
        Check((await save.Execute(current.Id, actor, input with { Fields = null, Version = null })).Failure is null, "legacy PUT still accepts omitted revision");
        store.Conflict = true;
        Check((await save.Execute(current.Id, actor, input with { Fields = null, Version = null })).Failure == CharacterWriteFailure.Conflict, "commit-time conflict is propagated");
        store.Conflict = false;

        var plugin = new RPGSystemPlugin(new("write_fixture", "Fixture", "v1", "1"), "write-fixture", "custom", [1], [1], [], []);
        var registry = new RpgPluginRegistry([plugin], [new WritePlugin()]);
        var custom = new SystemDefinition([new("counter", "Counter", "number", "Stats", 0, 100, null)], null, "write_fixture", 1, "v1");
        store.Context = context with { System = custom };
        var customInput = input with { Fields = new() { ["counter"] = JsonSerializer.SerializeToElement(4) }, Version = null };
        Check((await new CreateCharacter(store, registry).Execute(context.CampaignId, actor, customInput)).Failure == CharacterWriteFailure.Invalid, "creation enforces injected plugin invariants");
        store.Current = current with { System = custom, State = current.State with { Fields = new() { ["counter"] = JsonSerializer.SerializeToElement(1) } } };
        Check((await new SaveCharacter(store, registry).Execute(current.Id, actor, customInput)).Failure == CharacterWriteFailure.Invalid, "ordinary save enforces injected plugin invariants without running an action");
        var read = await new ReadCharacter(store, registry).Sheet(current.Id, actor);
        Check(read is not null && read.RulesAvailable && read.State.Fields!["counter"].GetInt32() == 1,
            "reading a sheet does not invoke rule description or execution");
        Console.WriteLine("Character application: creation/save ports, ownership, roles, formula isolation, legacy fields, strict v1 revisions and plugin validation passed.");
    }

    private sealed class MemoryWriter(Guid actor, CharacterCreationContext context) : ICharacterWriteStore
    {
        public CharacterCreationContext Context = context;
        public CharacterSheetSnapshot Current = null!;
        public bool Conflict;
        public int Writes;
        public Task<CharacterCreationContext?> LoadCreationContext(Guid campaignId, Guid userId, CancellationToken ct) =>
            Task.FromResult<CharacterCreationContext?>(userId == actor && campaignId == Context.CampaignId ? Context : null);
        public Task<CharacterSheetSnapshot?> Load(Guid id, Guid userId, CancellationToken ct) => Task.FromResult<CharacterSheetSnapshot?>(userId == actor && Current.Id == id ? Current : null);
        public Task<CharacterWriteResponse> Create(CharacterCreationContext creation, CharacterSheetState state, Guid actorId, CancellationToken ct)
        {
            Writes++;
            Current = new(Guid.NewGuid(), creation.CampaignId, actorId, creation.Role, state, creation.System, DateTime.UtcNow, creation.SystemId);
            return Task.FromResult(new CharacterWriteResponse(Current));
        }
        public Task<CharacterWriteResponse> Save(CharacterSheetSnapshot before, CharacterSheetState state, Guid actorId, CancellationToken ct)
        {
            if (Conflict) return Task.FromResult(new CharacterWriteResponse(Failure: CharacterWriteFailure.Conflict));
            Writes++; Current = before with { State = state with { Version = before.State.Version + 1 } };
            return Task.FromResult(new CharacterWriteResponse(Current));
        }
    }

    private sealed class WritePlugin : IRpgRulesPlugin
    {
        public string RulesetId => "write-fixture";
        public RuleView Describe(SystemDefinition system, CharacterSheetState state) => throw new NotSupportedException();
        public RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int, int> draw) => throw new NotSupportedException();
        public string? ValidateStateChange(SystemDefinition system, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after, bool master, bool ruleExecution, bool newCharacter) =>
            after.TryGetValue("counter", out var value) && value.TryGetInt32(out var number) && number <= 3 ? null : "Counter exceeds plugin limit.";
    }
}
