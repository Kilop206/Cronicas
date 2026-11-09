using System.Text.Json;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;

public static class CharacterLifecycleTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Lifecycle: " + label); }
        var actor = Guid.NewGuid(); var id = Guid.NewGuid(); var campaign = Guid.NewGuid();
        var schema = new SystemDefinition([
            new("score", "Score", "number", "Stats", 0, 20, null),
            new("double_score", "Double", "number", "Stats", 0, 40, null, Formula: "score * 2"),
            new("gm_note", "Master", "text", "Notes", null, null, null, EditRole: "gm")
        ], null);
        var input = new CharacterSheetState("Character", null, null, 10, 20, 10, 20, 10, 20,
            new() { ["score"] = JsonSerializer.SerializeToElement(2), ["gm_note"] = JsonSerializer.SerializeToElement("current") }, 5);
        var current = new CharacterSheetSnapshot(id, campaign, actor, "player", input, schema, DateTime.UtcNow, Guid.NewGuid());
        var historicalFields = new Dictionary<string, JsonElement>(input.Fields!) { ["score"] = JsonSerializer.SerializeToElement(3),
            ["double_score"] = JsonSerializer.SerializeToElement(999), ["retired_field"] = JsonSerializer.SerializeToElement(new { text = "preserve" }) };
        var revision = new CharacterRevisionState(Guid.NewGuid(), id, input with { Name = "Earlier", Fields = historicalFields, Health = 15, Version = 1 });
        var store = new MemoryLifecycle(actor, current, revision);
        var queries = new ReadCharacterLifecycle(store);
        var list = (await queries.Campaign(campaign, actor))!;
        Check(list.Length == 2 && list[0].CanEdit && !list[1].CanEdit, "list applies permission per owner");
        Check(await queries.Campaign(Guid.NewGuid(), actor) is null, "campaign list distinguishes inaccessible campaign");
        Check((await queries.History(id, actor))!.Length == 1 && store.LastLimit == 100, "history is bounded and excludes snapshot contents");
        var duplicate = new DuplicateCharacter(store);
        var copied = await duplicate.Execute(id, actor);
        Check(copied.Failure is null && copied.Character!.Id != id && copied.Character.OwnerId == actor && copied.Character.State.Version == 1,
            "duplicate receives its own identity, owner and revision");
        Check(copied.Character!.State.Name == "Character (cópia)" && !ReferenceEquals(copied.Character.State.Fields, input.Fields), "duplicate fields are independent");
        store.Current = current with { Role = "spectator" };
        Check(await queries.History(id, actor) is null && (await duplicate.Execute(id, actor)).Failure == CharacterWriteFailure.NotFound, "spectator cannot use history or duplication");
        store.Current = current with { OwnerId = Guid.NewGuid() };
        Check(await queries.History(id, actor) is null && (await duplicate.Execute(id, actor)).Failure == CharacterWriteFailure.Forbidden, "player cannot inspect history or duplicate another player's sheet");
        store.Current = current;
        var restore = new RestoreCharacter(store, store, RpgPluginRegistry.Default);
        Check((await restore.Execute(id, revision.Id, 4, actor)).Failure == CharacterWriteFailure.Conflict, "restoration requires current revision");
        Check((await restore.Execute(id, Guid.NewGuid(), 5, actor)).Failure == CharacterWriteFailure.NotFound, "unknown historical revision is hidden");
        store.Revision = revision with { CharacterId = Guid.NewGuid() };
        Check((await restore.Execute(id, revision.Id, 5, actor)).Failure == CharacterWriteFailure.NotFound, "cross-character history cannot be applied");
        store.Revision = revision;
        var restored = await restore.Execute(id, revision.Id, 5, actor);
        Check(restored.Failure is null && restored.Character!.State.Version == 6 && restored.Character.State.Health == 15, "restore appends revision instead of rewinding its counter");
        Check(restored.Character!.State.Fields!["retired_field"].GetProperty("text").GetString() == "preserve" &&
            restored.Character.State.Fields["double_score"].GetInt32() == 6 && historicalFields["double_score"].GetInt32() == 999,
            "restore retains historical fields and recalculates formulas without modifying archive");
        store.Current = current;
        store.Revision = revision with { State = revision.State with { Fields = new(historicalFields) { ["gm_note"] = JsonSerializer.SerializeToElement("old master value") } } };
        Check((await restore.Execute(id, revision.Id, 5, actor)).Failure == CharacterWriteFailure.Invalid, "player cannot undo current GM field through history");
        store.Current = current with { Role = "gm" };
        Check((await restore.Execute(id, revision.Id, 5, actor)).Failure is null, "GM can restore authorized fields");
        store.Current = current;
        store.Revision = revision;
        store.SaveFailure = CharacterWriteFailure.Forbidden;
        Check((await restore.Execute(id, revision.Id, 5, actor)).Failure == CharacterWriteFailure.NotFound, "lost permission at commit keeps history inaccessible");
        store.SaveFailure = CharacterWriteFailure.Conflict;
        Check((await restore.Execute(id, revision.Id, 5, actor)).Failure == CharacterWriteFailure.Conflict, "commit-time revision conflict is propagated");
        Console.WriteLine("Character lifecycle application: lists, bounded history, duplication ownership/isolation, historical fields, formulas, GM access and restoration conflicts passed.");
    }

    private sealed class MemoryLifecycle(Guid actor, CharacterSheetSnapshot initial, CharacterRevisionState revision) : ICharacterLifecycleStore, ICharacterWriteStore
    {
        public CharacterSheetSnapshot Current = initial;
        public CharacterRevisionState Revision = revision;
        public int LastLimit;
        public CharacterWriteFailure? SaveFailure;
        public Task<CharacterSheetSnapshot?> Load(Guid id, Guid user, CancellationToken ct) => Task.FromResult<CharacterSheetSnapshot?>(id == Current.Id && user == actor ? Current : null);
        public Task<CampaignCharacterListing?> ListCampaign(Guid campaign, Guid user, CancellationToken ct) => Task.FromResult<CampaignCharacterListing?>(
            campaign == Current.CampaignId && user == actor ? new(Current.Role, [new(Current.Id, campaign, actor, Current.State, Current.UpdatedAt),
                new(Guid.NewGuid(), campaign, Guid.NewGuid(), Current.State, Current.UpdatedAt)]) : null);
        public Task<CharacterRevisionSummary[]?> ListRevisions(Guid id, Guid user, int limit, CancellationToken ct)
        { LastLimit = limit; return Task.FromResult<CharacterRevisionSummary[]?>([new(Revision.Id, 1, DateTime.UtcNow, "GM")]); }
        public Task<CharacterRevisionState?> LoadRevision(Guid id, Guid revisionId, Guid user, CancellationToken ct) =>
            Task.FromResult<CharacterRevisionState?>(revisionId == Revision.Id ? Revision : null);
        public Task<CharacterWriteResponse> Duplicate(CharacterSheetSnapshot source, CharacterSheetState copy, Guid user, CancellationToken ct) =>
            Task.FromResult(new CharacterWriteResponse(source with { Id = Guid.NewGuid(), OwnerId = user, State = copy }));
        public Task<CharacterWriteResponse> Save(CharacterSheetSnapshot before, CharacterSheetState state, Guid user, CancellationToken ct)
        {
            if (SaveFailure is { } failure) return Task.FromResult(new CharacterWriteResponse(Failure: failure));
            Current = before with { State = state with { Version = before.State.Version + 1 } };
            return Task.FromResult(new CharacterWriteResponse(Current));
        }
        public Task<CharacterCreationContext?> LoadCreationContext(Guid campaignId, Guid user, CancellationToken ct) => throw new NotSupportedException();
        public Task<CharacterWriteResponse> Create(CharacterCreationContext context, CharacterSheetState state, Guid user, CancellationToken ct) => throw new NotSupportedException();
    }
}
