using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed class ManageCharacterFavorites(ICharacterFavoriteStore store)
{
    public Task<CharacterFavoriteDocument?> Read(Guid character, Guid actor, CancellationToken ct = default) => store.ReadFavorite(character, actor, ct);
    public async Task<CharacterAssetResult<CharacterFavoriteDocument>> Set(Guid character, Guid actor, bool favorite, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (access is null) return new(Failure: CharacterAssetFailure.NotFound);
        return await store.SetFavorite(access, actor, favorite, ct);
    }
}
