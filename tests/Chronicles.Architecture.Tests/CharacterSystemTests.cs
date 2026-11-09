using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;

public static class CharacterSystemTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Character system: " + label); }
        var actor = Guid.NewGuid(); var system = Guid.NewGuid();
        var schema = new SystemDefinition([new("score", "Original", "number", "Stats", 0, 20, null)], null, Revision: 3,
            NpcTemplates: [new("private_npc", "Private NPC", Sheet: new())]);
        var frozen = FrozenSystemTemplate.Capture("Template", "Description", schema);
        Check(frozen.Definition.NpcTemplates is null && frozen.Definition.History is null, "snapshot excludes private NPC catalog and mutable history");
        Check(frozen.Fingerprint() == FrozenSystemTemplate.Capture("Template", "Description", schema with { NpcTemplates = [] }).Fingerprint(), "non-character catalogs do not change fingerprint");
        schema.Fields[0] = schema.Fields[0] with { Label = "Changed" };
        Check(frozen.Definition.Fields[0].Label == "Original" && frozen.Fingerprint() != FrozenSystemTemplate.Capture("Template", "Description", schema).Fingerprint(), "snapshot is a deep independent definition");
        var current = new CharacterSheetSnapshot(Guid.NewGuid(), Guid.NewGuid(), actor, "player", new("Character", null, null, 1, 2, 1, 2, 1, 2, Version: 4), schema, DateTime.UtcNow, system);
        var store = new MemoryStore(current); var pin = new PinCharacterSystem(store);
        Check((await pin.Execute(current.Id, actor, 4, system, 3)).Failure == CharacterWriteFailure.Forbidden && store.Pins == 0, "player cannot freeze own sheet rules");
        store.Current = current with { Role = "co-gm" };
        Check((await pin.Execute(current.Id, actor, 3, system, 3)).Failure == CharacterWriteFailure.Conflict, "stale character version denied");
        Check((await pin.Execute(current.Id, actor, 4, Guid.NewGuid(), 3)).Failure == CharacterWriteFailure.Conflict, "source system identity must match");
        Check((await pin.Execute(current.Id, actor, 4, system, 2)).Failure == CharacterWriteFailure.Conflict, "source template revision must match");
        var result = await pin.Execute(current.Id, actor, 4, system, 3);
        Check(result.Character is { SystemVersionId: not null } && result.Character.State.Version == 5 && store.Pins == 1, "co-GM pins through persistence port");
        Check((await pin.Execute(current.Id, actor, 5, system, 3)).Failure is null && store.Pins == 1, "already bound state is idempotent");
        var envelope = CharacterSheetProjection.Envelope(result.Character!, actor, RpgPluginRegistry.Default);
        Check(envelope.SystemVersionId == result.Character!.SystemVersionId && envelope.TemplateRevision == 3, "envelope exposes exact binding");
        Console.WriteLine("Character systems application: immutable snapshots, private catalog exclusion, source identity/revision, master-only pin and binding envelope passed.");
    }

    private sealed class MemoryStore(CharacterSheetSnapshot current) : ICharacterSystemStore
    {
        public CharacterSheetSnapshot Current = current; public int Pins;
        public Task<CharacterSheetSnapshot?> Load(Guid character, Guid actor, CancellationToken ct) => Task.FromResult<CharacterSheetSnapshot?>(Current);
        public Task<CharacterSystemRead> ReadSystem(Guid character, Guid actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<CharacterWriteResponse> Pin(CharacterSheetSnapshot before, Guid actor, CancellationToken ct)
        { Pins++; Current = before with { SystemVersionId = Guid.NewGuid(), State = before.State with { Version = before.State.Version + 1 } }; return Task.FromResult(new CharacterWriteResponse(Current)); }
    }
}
