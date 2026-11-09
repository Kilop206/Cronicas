using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed class ManageCharacterInventory(ICharacterInventoryStore store)
{
    private const string Denied = "Você não pode editar este inventário.";
    private const string Changed = "O item mudou. Atualize antes de salvar.";
    public Task<InventoryItemDocument[]?> List(Guid character, Guid actor, CancellationToken ct = default) => store.List(character, actor, ct);

    public async Task<CharacterAssetResult<InventoryItemDocument>> Create(Guid character, Guid actor, InventoryItemValues values, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<InventoryItemDocument>(access, actor, Denied) is { } error) return error;
        if (values.Validate() is { } errors) return new(Failure: CharacterAssetFailure.Invalid, Errors: errors);
        return await store.Create(access!, values.Normalize(), actor, ct);
    }

    public async Task<CharacterAssetResult<InventoryItemDocument>> Update(Guid character, Guid item, Guid actor,
        int version, InventoryItemValues values, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<InventoryItemDocument>(access, actor, Denied) is { } error) return error;
        var before = await store.Find(character, item, actor, ct);
        if (before is null) return new(Failure: CharacterAssetFailure.NotFound);
        if (before.Version != version) return new(Failure: CharacterAssetFailure.Conflict, Message: Changed);
        if (values.Validate() is { } errors) return new(Failure: CharacterAssetFailure.Invalid, Errors: errors);
        return await store.Update(access!, before, values.Normalize(), actor, ct);
    }

    public async Task<CharacterAssetResult<InventoryItemDocument>> Delete(Guid character, Guid item, Guid actor, int version, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (CharacterAssetAuthorization.Error<InventoryItemDocument>(access, actor, Denied) is { } error) return error;
        var before = await store.Find(character, item, actor, ct);
        if (before is null) return new(Failure: CharacterAssetFailure.NotFound);
        if (before.Version != version) return new(Failure: CharacterAssetFailure.Conflict, Message: "O item mudou. Atualize antes de excluir.");
        return await store.Delete(access!, before, actor, ct);
    }
}
