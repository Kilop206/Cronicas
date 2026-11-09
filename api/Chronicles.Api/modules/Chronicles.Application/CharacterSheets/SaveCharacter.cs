using System.Text.Json;
using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class SaveCharacter(ICharacterWriteStore store, RpgPluginRegistry plugins)
{
    public async Task<CharacterWriteResponse> Execute(Guid id, Guid actorId, CharacterSheetState input,
        bool requireRevision = false, CancellationToken ct = default)
    {
        var current = await store.Load(id, actorId, ct);
        if (current is null) return new(Failure: CharacterWriteFailure.NotFound);
        if (!CharacterAccessPolicy.CanEdit(current.OwnerId, actorId, current.Role))
            return new(Failure: CharacterWriteFailure.Forbidden, Message: "Você não pode editar este personagem.");
        if (requireRevision && input.Version is not >= 1)
            return new(Failure: CharacterWriteFailure.Invalid, Errors: new() { ["version"] = ["Informe a revisão atual da ficha."] });
        if (input.Version is { } expected && expected != current.State.Version)
            return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha mudou. Atualize antes de salvar.");
        var errors = CharacterSheetValidation.Validate(input);
        if (errors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: errors);
        // Neither omission of fields nor formula calculation may mutate the loaded snapshot or caller's draft.
        var values = CharacterWriteValues.Copy(input.Fields ?? current.State.Fields);
        var fieldErrors = CharacterSheetValidation.ValidateFields(current.System, values, current.State.Fields,
            CharacterAccessPolicy.IsMaster(current.Role), ruleValidator: current.System is null ? null : plugins.RulesFor(current.System));
        if (fieldErrors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: fieldErrors);
        return await store.Save(current, CharacterWriteValues.Normalize(input, values) with { Version = current.State.Version }, actorId, ct);
    }
}

internal static class CharacterWriteValues
{
    internal static Dictionary<string, JsonElement> Copy(Dictionary<string, JsonElement>? values) =>
        values?.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal) ?? [];

    internal static CharacterSheetState Normalize(CharacterSheetState input, Dictionary<string, JsonElement> fields) =>
        input with { Name = input.Name.Trim(), PortraitUrl = input.PortraitUrl?.Trim(), BackgroundUrl = input.BackgroundUrl?.Trim(), Fields = fields };
}
