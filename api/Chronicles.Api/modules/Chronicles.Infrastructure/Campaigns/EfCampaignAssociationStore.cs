using Chronicles.Api.Data;
using Chronicles.Application.Campaigns;
using Chronicles.Application.RpgSystems;
using Chronicles.Application.Worlds;
using Chronicles.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Campaigns;

public sealed class EfCampaignAssociationStore(AppDbContext db, IRpgSystemCampaignAccess systems, IWorldCampaignAccess worlds) : ICampaignAssociationStore
{
    public Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct) => CampaignPersistence.Read(db, campaign, actor, ct);

    public async Task<CampaignResult<bool>> Change(CampaignContext before, Guid actor, CampaignAssociationChange change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var id = before.Document.Id;
            await CampaignPersistence.LockMember(db, id, actor, ct);
            var rows = await db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct);
            var fresh = await Read(id, actor, ct);
            if (fresh is null || !CampaignPolicy.IsOwner(fresh.OwnerId, actor, fresh.Document.Role)) return new(Failure: CampaignFailure.NotFound);
            if (fresh.OwnerId != before.OwnerId || fresh.Document.Role != before.Document.Role) return CampaignPersistence.Changed<bool>();
            if (change.Kind is not (CampaignAssociationKind.System or CampaignAssociationKind.World)) return new(Failure: CampaignFailure.Invalid);
            var current = change.Kind == CampaignAssociationKind.System ? fresh.Document.SystemId : fresh.Document.WorldId;
            var expected = change.Kind == CampaignAssociationKind.System ? before.Document.SystemId : before.Document.WorldId;
            // Independent changes to system and world may both succeed; stale decisions about the same link may not.
            if (current != expected) return CampaignPersistence.Changed<bool>();
            if (change.TargetId is { } target && !(change.Kind == CampaignAssociationKind.System
                ? await systems.HoldOwnedReference(target, actor, ct) : await worlds.HoldOwnedReference(target, actor, ct)))
                return ManageCampaignAssociations.Unavailable(change.Kind);
            if (current == change.TargetId) return new(false);
            var campaign = rows.Single();
            if (change.Kind == CampaignAssociationKind.System) campaign.SystemId = change.TargetId;
            else campaign.WorldId = change.TargetId;
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(true);
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<bool>(); }
    }
}
