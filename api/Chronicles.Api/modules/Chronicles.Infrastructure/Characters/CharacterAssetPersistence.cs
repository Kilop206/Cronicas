using Chronicles.Api.Data;
using Chronicles.Application.Characters;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

internal static class CharacterAssetPersistence
{
    internal static Task<CharacterAssetAccess?> LoadAccess(AppDbContext db, Guid character, Guid actor, CancellationToken ct) =>
        db.Characters.AsNoTracking().Where(c => c.Id == character && c.Campaign.Memberships.Any(m => m.UserId == actor))
            .Select(c => new CharacterAssetAccess(c.Id, c.CampaignId, c.OwnerId,
                c.Campaign.Memberships.Where(m => m.UserId == actor).Select(m => m.Role).First())).SingleOrDefaultAsync(ct);

    internal static async Task<CharacterAssetResult<T>> Write<T>(AppDbContext db, CharacterAssetAccess before, Guid actor,
        string denied, string changed, Func<Task<CharacterAssetResult<T>>> mutate, CancellationToken ct,
        CharacterAccessRequirement requirement = CharacterAccessRequirement.Edit)
    {
        // ReadCommitted is intentional: after waiting for the parent lock, capacity counts must see the last commit.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            await db.CampaignMemberships.FromSqlInterpolated($"SELECT * FROM campaign_memberships WHERE \"CampaignId\" = {before.CampaignId} AND \"UserId\" = {actor} FOR SHARE")
                .AsNoTracking().ToListAsync(ct);
            await db.Characters.FromSqlInterpolated($"SELECT * FROM characters WHERE \"Id\" = {before.CharacterId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
            var fresh = await LoadAccess(db, before.CharacterId, actor, ct);
            if (fresh is null) return new(Failure: CharacterAssetFailure.NotFound);
            if (!CharacterAccessPolicy.Allows(requirement, fresh.OwnerId, actor, fresh.Role))
                return new(Failure: requirement is CharacterAccessRequirement.Master or CharacterAccessRequirement.Owner
                    ? CharacterAssetFailure.NotFound : CharacterAssetFailure.Forbidden, Message: denied);
            if (fresh != before) return new(Failure: CharacterAssetFailure.Conflict, Message: changed);
            var result = await mutate();
            if (result.Failure is not null) return result;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error))
        {
            db.ChangeTracker.Clear();
            return new(Failure: CharacterAssetFailure.Conflict, Message: changed);
        }
    }
}
