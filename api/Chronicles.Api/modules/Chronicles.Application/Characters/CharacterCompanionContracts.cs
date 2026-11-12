using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed record CharacterFavoriteDocument(bool Favorite);
public sealed record CharacterSecretDocument(string Notes, int Revision, DateTime? UpdatedAt);
public sealed record CharacterOverlayKey(string Key);
public sealed record CharacterOverlaySource(CharacterSheetState State, SystemDefinition? System, DateTime UpdatedAt);
public sealed record CharacterOverlayResource(string Label, int Current, int Max);
public sealed record CharacterOverlayIndicator(string Label, string Value);
public sealed record CharacterOverlayDocument(string Name, string? PortraitUrl, string? BackgroundUrl, int Health, int MaxHealth,
    int Sanity, int MaxSanity, int Mana, int MaxMana, DateTime UpdatedAt, string? TemplateId,
    CharacterOverlayResource[] Resources, CharacterOverlayIndicator[] Indicators);

public interface ICharacterFavoriteStore : ICharacterAssetAccessReader
{
    Task<CharacterFavoriteDocument?> ReadFavorite(Guid character, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<CharacterFavoriteDocument>> SetFavorite(CharacterAssetAccess access, Guid actor, bool favorite, CancellationToken ct);
}

public interface ICharacterSecretStore : ICharacterAssetAccessReader
{
    Task<CharacterSecretDocument?> ReadSecret(Guid character, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<CharacterSecretDocument>> SaveSecret(CharacterAssetAccess access, CharacterSecretDocument before,
        CharacterSecretValues values, Guid actor, CancellationToken ct);
}

public interface ICharacterOverlayStore : ICharacterAssetAccessReader
{
    Task<CharacterOverlaySource?> ReadOverlay(string keyHash, CancellationToken ct);
    Task<CharacterAssetResult<bool>> RotateOverlay(CharacterAssetAccess access, string keyHash, Guid actor, CancellationToken ct);
}

public interface IOverlayKeyGenerator { string Generate(); }
