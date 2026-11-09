using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class ReadCharacter(ICharacterSheetReader reader, RpgPluginRegistry plugins)
{
    public async Task<CharacterDocument?> Document(Guid id, Guid actor, CancellationToken ct = default)
    {
        var snapshot = await reader.Load(id, actor, ct);
        return snapshot is null ? null : CharacterSheetProjection.Document(snapshot, actor);
    }

    public async Task<CharacterSheetEnvelope?> Sheet(Guid id, Guid actor, CancellationToken ct = default)
    {
        var snapshot = await reader.Load(id, actor, ct);
        // Reading/resync must remain possible even when the state needs correction before a rule can run.
        return snapshot is null ? null : CharacterSheetProjection.Envelope(snapshot, actor, plugins);
    }
}
