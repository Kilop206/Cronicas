using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public sealed record CharacterAssetAccess(Guid CharacterId, Guid CampaignId, Guid OwnerId, string Role);
public enum CharacterAssetFailure { NotFound, Forbidden, Conflict, Invalid }
public sealed record CharacterAssetResult<T>(T? Value = default, CharacterAssetFailure? Failure = null,
    string? Message = null, Dictionary<string, string[]>? Errors = null);
public sealed record InventoryItemDocument(Guid Id, Guid CharacterId, string Name, string Category, int Quantity,
    decimal Weight, string Notes, int Version, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record CharacterAbilityDocument(Guid Id, Guid CharacterId, string Name, string Description, string Cost, string Effect,
    string Duration, string Limitations, string Recharge, string? RollExpression, int Version, DateTime CreatedAt, DateTime UpdatedAt);

public interface ICharacterAssetAccessReader
{
    Task<CharacterAssetAccess?> LoadAccess(Guid characterId, Guid actor, CancellationToken ct);
}

public interface ICharacterInventoryStore : ICharacterAssetAccessReader
{
    Task<InventoryItemDocument[]?> List(Guid characterId, Guid actor, CancellationToken ct);
    Task<InventoryItemDocument?> Find(Guid characterId, Guid itemId, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<InventoryItemDocument>> Create(CharacterAssetAccess access, InventoryItemValues values, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<InventoryItemDocument>> Update(CharacterAssetAccess access, InventoryItemDocument before, InventoryItemValues values, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<InventoryItemDocument>> Delete(CharacterAssetAccess access, InventoryItemDocument before, Guid actor, CancellationToken ct);
}

public interface ICharacterAbilityStore : ICharacterAssetAccessReader
{
    Task<CharacterAbilityDocument[]?> List(Guid characterId, Guid actor, CancellationToken ct);
    Task<CharacterAbilityDocument?> Find(Guid characterId, Guid abilityId, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<CharacterAbilityDocument>> Create(CharacterAssetAccess access, CharacterAbilityValues values, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<CharacterAbilityDocument>> Update(CharacterAssetAccess access, CharacterAbilityDocument before, CharacterAbilityValues values, Guid actor, CancellationToken ct);
    Task<CharacterAssetResult<CharacterAbilityDocument>> Delete(CharacterAssetAccess access, CharacterAbilityDocument before, Guid actor, CancellationToken ct);
}

internal static class CharacterAssetAuthorization
{
    internal static CharacterAssetResult<T>? Error<T>(CharacterAssetAccess? access, Guid actor, string message) => access is null
        ? new(Failure: CharacterAssetFailure.NotFound)
        : !CharacterAccessPolicy.CanEdit(access.OwnerId, actor, access.Role) ? new(Failure: CharacterAssetFailure.Forbidden, Message: message) : null;
}
