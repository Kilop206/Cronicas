using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed class ManageCharacterOverlay(ICharacterOverlayStore store, IOverlayKeyGenerator keys, RpgPluginRegistry plugins)
{
    public async Task<CharacterOverlayDocument?> Read(string key, CancellationToken ct = default)
    {
        if (!OverlayCapability.IsValid(key)) return null;
        var source = await store.ReadOverlay(OverlayCapability.Hash(key), ct);
        return source is null ? null : CharacterOverlayProjection.Document(source, plugins);
    }

    public async Task<CharacterAssetResult<CharacterOverlayKey>> Rotate(Guid character, Guid actor, CancellationToken ct = default)
    {
        var access = await store.LoadAccess(character, actor, ct);
        if (access is null || !CharacterAccessPolicy.Allows(CharacterAccessRequirement.Owner, access.OwnerId, actor, access.Role))
            return new(Failure: CharacterAssetFailure.NotFound);
        var key = keys.Generate();
        var hash = OverlayCapability.Hash(key);
        var saved = await store.RotateOverlay(access, hash, actor, ct);
        return saved.Failure is { } failure ? new(Failure: failure, Message: saved.Message, Errors: saved.Errors) : new(new(key));
    }
}
