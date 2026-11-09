using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterSystemRevisionSummary(int Revision, DateTime SavedAt, string Name);
public sealed record CharacterSystemDocument(Guid Id, Guid? OwnerId, string Name, string Description, SystemField[] Fields,
    SheetLayout? Layout, string? TemplateId, int TemplateVersion, string? RulesetVersion, string? RendererMode, int Revision,
    CharacterSystemRevisionSummary[] Revisions, NpcTemplate[] NpcTemplates, CthulhuOccupationDefinition? CthulhuOccupations,
    DateTime CreatedAt, DateTime UpdatedAt, Guid? SystemVersionId);
public sealed record CharacterSystemRead(bool Accessible, CharacterSystemDocument? System);

public interface ICharacterSystemStore : ICharacterSheetReader
{
    Task<CharacterSystemRead> ReadSystem(Guid character, Guid actor, CancellationToken ct);
    Task<CharacterWriteResponse> Pin(CharacterSheetSnapshot before, Guid actor, CancellationToken ct);
}

public sealed class ReadCharacterSystem(ICharacterSystemStore store)
{
    public Task<CharacterSystemRead> Read(Guid character, Guid actor, CancellationToken ct = default) => store.ReadSystem(character, actor, ct);
}

public sealed class PinCharacterSystem(ICharacterSystemStore store)
{
    public async Task<CharacterWriteResponse> Execute(Guid character, Guid actor, int expectedVersion, Guid registrySystemId, int templateRevision, CancellationToken ct = default)
    {
        var before = await store.Load(character, actor, ct);
        if (before is null) return new(Failure: CharacterWriteFailure.NotFound);
        if (!CharacterAccessPolicy.IsMaster(before.Role)) return new(Failure: CharacterWriteFailure.Forbidden, Message: "Somente mestres fixam a versão do sistema.");
        if (before.System is null || registrySystemId == Guid.Empty || expectedVersion < 1 || templateRevision < 1)
            return new(Failure: CharacterWriteFailure.Invalid, Errors: new() { ["system"] = ["Informe o sistema e as revisões atuais antes de fixar a versão."] });
        if (before.State.Version != expectedVersion || before.SystemId != registrySystemId || before.System?.Revision != templateRevision)
            return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha ou o sistema mudou. Recarregue antes de fixar a versão.");
        if (before.SystemVersionId is not null) return new(before);
        return await store.Pin(before, actor, ct);
    }
}
