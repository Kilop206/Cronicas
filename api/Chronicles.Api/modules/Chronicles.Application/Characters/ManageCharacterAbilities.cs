using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed class ManageCharacterAbilities(ICharacterAbilityStore store)
{
    private const string Denied = "Você não pode editar as habilidades deste personagem.";
    private const string Changed = "A habilidade mudou. Atualize antes de salvar.";
    public Task<CharacterAbilityDocument[]?> List(Guid character, Guid actor, CancellationToken ct = default) => store.List(character, actor, ct);

    public async Task<CharacterAssetResult<CharacterAbilityDocument>> Create(Guid character, Guid actor, CharacterAbilityValues values, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<CharacterAbilityDocument>(access, actor, Denied) is { } error) return error;
        if (values.Validate() is { } errors) return new(Failure: CharacterAssetFailure.Invalid, Errors: errors);
        return await store.Create(access!, values.Normalize(), actor, ct);
    }

    public async Task<CharacterAssetResult<CharacterAbilityDocument>> Update(Guid character, Guid ability, Guid actor,
        int version, CharacterAbilityValues values, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<CharacterAbilityDocument>(access, actor, Denied) is { } error) return error;
        var before = await store.Find(character, ability, actor, ct);
        if (before is null) return new(Failure: CharacterAssetFailure.NotFound);
        if (before.Version != version) return new(Failure: CharacterAssetFailure.Conflict, Message: Changed);
        if (values.Validate() is { } errors) return new(Failure: CharacterAssetFailure.Invalid, Errors: errors);
        return await store.Update(access!, before, values.Normalize(), actor, ct);
    }

    public async Task<CharacterAssetResult<CharacterAbilityDocument>> Delete(Guid character, Guid ability, Guid actor, int version, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<CharacterAbilityDocument>(access, actor, Denied) is { } error) return error;
        var before = await store.Find(character, ability, actor, ct);
        if (before is null) return new(Failure: CharacterAssetFailure.NotFound);
        if (before.Version != version) return new(Failure: CharacterAssetFailure.Conflict, Message: "A habilidade mudou. Atualize antes de excluir.");
        return await store.Delete(access!, before, actor, ct);
    }
}
