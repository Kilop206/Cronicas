using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class DuplicateCharacter(ICharacterLifecycleStore store)
{
    public async Task<CharacterWriteResponse> Execute(Guid id, Guid actor, CancellationToken ct = default)
    {
        var source = await store.Load(id, actor, ct);
        if (source is null || source.Role == "spectator") return new(Failure: CharacterWriteFailure.NotFound);
        if (!CharacterAccessPolicy.CanEdit(source.OwnerId, actor, source.Role))
            return new(Failure: CharacterWriteFailure.Forbidden, Message: "Você não pode duplicar este personagem.");
        var length = Math.Min(source.State.Name.Length, 110);
        if (length > 0 && char.IsHighSurrogate(source.State.Name[length - 1])) length--;
        var copy = source.State with { Name = source.State.Name[..length] + " (cópia)",
            Fields = CharacterWriteValues.Copy(source.State.Fields), Version = 1 };
        // Copy the trusted persisted state exactly; creation rules must not discard historical fields.
        return await store.Duplicate(source, copy, actor, ct);
    }
}
