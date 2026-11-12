using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterCompanionStore(AppDbContext db) : ICharacterFavoriteStore, ICharacterSecretStore, ICharacterOverlayStore
{
    public Task<CharacterAssetAccess?> LoadAccess(Guid character, Guid actor, CancellationToken ct) => CharacterAssetPersistence.LoadAccess(db, character, actor, ct);

    public Task<CharacterFavoriteDocument?> ReadFavorite(Guid character, Guid actor, CancellationToken ct) => db.Characters.AsNoTracking()
        .Where(c => c.Id == character && c.Campaign.Memberships.Any(m => m.UserId == actor))
        .Select(c => new CharacterFavoriteDocument(db.CharacterFavorites.Any(f => f.CharacterId == c.Id && f.UserId == actor))).SingleOrDefaultAsync(ct);

    public Task<CharacterAssetResult<CharacterFavoriteDocument>> SetFavorite(CharacterAssetAccess access, Guid actor, bool favorite, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, "", "A participação mudou. Atualize antes de alterar os favoritos.", async () =>
        {
            var existing = await db.CharacterFavorites.SingleOrDefaultAsync(f => f.CharacterId == access.CharacterId && f.UserId == actor, ct);
            if (favorite && existing is null) db.CharacterFavorites.Add(new() { CharacterId = access.CharacterId, UserId = actor });
            if (!favorite && existing is not null) db.CharacterFavorites.Remove(existing);
            return new CharacterAssetResult<CharacterFavoriteDocument>(new(favorite));
        }, ct, CharacterAccessRequirement.Member);

    private IQueryable<Guid> MasterCharacters(Guid actor) => db.Characters.Where(c => c.Campaign.Memberships.Any(m => m.UserId == actor &&
        (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm"))).Select(c => c.Id);

    public async Task<CharacterSecretDocument?> ReadSecret(Guid character, Guid actor, CancellationToken ct)
    {
        // One SQL statement ensures both authorization and contents use the same committed view.
        var row = await db.Characters.AsNoTracking().Where(c => c.Id == character && MasterCharacters(actor).Contains(c.Id))
            .Select(c => new { Secret = db.CharacterSecrets.Where(s => s.CharacterId == c.Id).Select(s => new CharacterSecretDocument(s.Notes, s.Revision, s.UpdatedAt)).FirstOrDefault() })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : row.Secret ?? new("", 1, null);
    }

    public Task<CharacterAssetResult<CharacterSecretDocument>> SaveSecret(CharacterAssetAccess access, CharacterSecretDocument before,
        CharacterSecretValues values, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, "", "As anotações mudaram. Atualize antes de salvar.", async () =>
        {
            var secret = await db.CharacterSecrets.SingleOrDefaultAsync(s => s.CharacterId == access.CharacterId, ct);
            if ((secret is null) != (before.UpdatedAt is null) || values.ExpectedExists is { } exists && exists != (secret is not null) ||
                (secret?.Revision ?? 1) != values.Revision ||
                secret is not null && secret.Revision != before.Revision)
                return new(Failure: CharacterAssetFailure.Conflict, Message: "As anotações mudaram. Atualize antes de salvar.");
            if (secret is null) { secret = new() { CharacterId = access.CharacterId, Notes = values.Notes }; db.CharacterSecrets.Add(secret); }
            else { secret.Notes = values.Notes; secret.Revision++; secret.UpdatedAt = DateTime.UtcNow; }
            return new CharacterAssetResult<CharacterSecretDocument>(new(secret.Notes, secret.Revision, secret.UpdatedAt));
        }, ct, CharacterAccessRequirement.Master);

    public async Task<CharacterOverlaySource?> ReadOverlay(string keyHash, CancellationToken ct)
    {
        var row = await db.Characters.AsNoTracking().Where(c => c.OverlayKeyHash == keyHash)
            .Select(c => new { Character = c, Schema = c.SystemVersion == null ? c.Campaign.System == null ? null : c.Campaign.System.SchemaJson : c.SystemVersion.DefinitionJson }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(CharacterSheetPersistence.State(row.Character), row.Schema is null ? null : SystemDefinition.Read(row.Schema), row.Character.UpdatedAt);
    }

    public Task<CharacterAssetResult<bool>> RotateOverlay(CharacterAssetAccess access, string keyHash, Guid actor, CancellationToken ct) =>
        CharacterAssetPersistence.Write(db, access, actor, "", "A ficha mudou. Atualize antes de gerar uma nova URL.", async () =>
        {
            var character = await db.Characters.SingleAsync(c => c.Id == access.CharacterId, ct);
            character.OverlayKeyHash = keyHash;
            return new CharacterAssetResult<bool>(true);
        }, ct, CharacterAccessRequirement.Owner);
}
