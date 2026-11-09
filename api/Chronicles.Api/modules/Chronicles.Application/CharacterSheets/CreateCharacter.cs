using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class CreateCharacter(ICharacterWriteStore store, RpgPluginRegistry plugins)
{
    public async Task<CharacterWriteResponse> Execute(Guid campaignId, Guid actorId, CharacterSheetState input, CancellationToken ct = default)
    {
        var context = await store.LoadCreationContext(campaignId, actorId, ct);
        if (context is null) return new(Failure: CharacterWriteFailure.NotFound);
        if (context.Role == "spectator") return new(Failure: CharacterWriteFailure.Forbidden,
            Message: "Espectadores não podem criar personagens.");
        var errors = CharacterSheetValidation.Validate(input);
        if (errors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: errors);
        var values = CharacterWriteValues.Copy(input.Fields);
        var fieldErrors = CharacterSheetValidation.ValidateFields(context.System, values,
            allowGm: CharacterAccessPolicy.IsMaster(context.Role), newCharacter: true,
            ruleValidator: context.System is null ? null : plugins.RulesFor(context.System));
        if (fieldErrors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: fieldErrors);
        return await store.Create(context, CharacterWriteValues.Normalize(input, values) with { Version = 1 }, actorId, ct);
    }
}
