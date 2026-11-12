using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed class ManageCharacterSecrets(ICharacterSecretStore store)
{
    public async Task<CharacterSecretDocument?> Read(Guid character, Guid actor, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        return access is null || !CharacterAccessPolicy.IsMaster(access.Role) ? null : await store.ReadSecret(character, actor, ct);
    }

    public async Task<CharacterAssetResult<CharacterSecretDocument>> Save(Guid character, Guid actor, CharacterSecretValues values, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (access is null || !CharacterAccessPolicy.IsMaster(access.Role)) return new(Failure: CharacterAssetFailure.NotFound);
        if (values.Validate() is { } errors) return new(Failure: CharacterAssetFailure.Invalid, Errors: errors);
        var before = await store.ReadSecret(character, actor, ct);
        if (before is null) return new(Failure: CharacterAssetFailure.NotFound);
        if (values.Revision != before.Revision || values.ExpectedExists is { } exists && exists != (before.UpdatedAt is not null))
            return new(Failure: CharacterAssetFailure.Conflict, Message: "As anotações mudaram. Atualize antes de salvar.");
        return await store.SaveSecret(access, before, values with { Notes = values.Notes.Trim() }, actor, ct);
    }
}
