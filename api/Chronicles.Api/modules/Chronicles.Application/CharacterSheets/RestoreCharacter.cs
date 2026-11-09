using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class RestoreCharacter(ICharacterLifecycleStore history, ICharacterWriteStore writer, RpgPluginRegistry plugins)
{
    public async Task<CharacterWriteResponse> Execute(Guid id, Guid revisionId, int expectedVersion, Guid actor, CancellationToken ct = default)
    {
        var current = await history.Load(id, actor, ct);
        if (current is null || !CharacterAccessPolicy.CanEdit(current.OwnerId, actor, current.Role))
            return new(Failure: CharacterWriteFailure.NotFound);
        if (expectedVersion != current.State.Version)
            return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha mudou. Atualize antes de restaurar.");
        var revision = await history.LoadRevision(id, revisionId, actor, ct);
        if (revision is null || revision.CharacterId != id) return new(Failure: CharacterWriteFailure.NotFound);
        var errors = CharacterSheetValidation.Validate(revision.State);
        if (errors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: errors);
        var restored = CharacterWriteValues.Copy(revision.State.Fields);
        // Unknown historical fields may be restored unchanged. GM access is checked against CURRENT values.
        var fieldErrors = CharacterSheetValidation.ValidateFields(current.System, restored, revision.State.Fields,
            CharacterAccessPolicy.IsMaster(current.Role), authorizationValues: current.State.Fields,
            ruleValidator: current.System is null ? null : plugins.RulesFor(current.System));
        if (fieldErrors is not null) return new(Failure: CharacterWriteFailure.Invalid, Errors: fieldErrors);
        var result = await writer.Save(current, CharacterWriteValues.Normalize(revision.State, restored) with { Version = current.State.Version }, actor, ct);
        return result.Failure == CharacterWriteFailure.Forbidden ? new(Failure: CharacterWriteFailure.NotFound) : result;
    }
}
