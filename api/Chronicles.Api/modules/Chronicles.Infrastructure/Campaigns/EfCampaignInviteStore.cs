using System.Security.Cryptography;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Campaigns;
using Chronicles.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Campaigns;

public sealed class CryptographicCampaignInviteTokenGenerator : ICampaignInviteTokenGenerator
{
    public string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}

public sealed class EfCampaignInviteStore(AppDbContext db) : ICampaignInviteStore
{
    public Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct) => CampaignPersistence.Read(db, campaign, actor, ct);
    public async Task<CampaignInvitePage> List(Guid campaign, Guid actor, int page, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = db.CampaignInvites.AsNoTracking().Where(i => i.CampaignId == campaign && i.CreatedById == actor &&
            i.Campaign.OwnerId == actor && i.Campaign.Memberships.Any(m => m.UserId == actor && m.Role == "owner") &&
            i.UsedAt == null && i.ExpiresAt > now && (i.Role == "player" || i.Role == "gm" || i.Role == "co-gm" || i.Role == "spectator"));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).Skip((page - 1) * 20).Take(20)
            .Select(i => new CampaignInviteSummary(i.Id, i.Role, i.CreatedAt, i.ExpiresAt)).ToArrayAsync(ct);
        return new(items, total, page);
    }
    public async Task<CampaignResult<bool>> Revoke(CampaignContext before, Guid actor, Guid invite, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var campaign = before.Document.Id;
            // Match acceptance lock order: invitation, owner membership, campaign.
            var matches = await db.CampaignInvites.FromSqlInterpolated($"SELECT * FROM campaign_invites WHERE \"Id\" = {invite} AND \"CampaignId\" = {campaign} FOR UPDATE").ToListAsync(ct);
            if (matches.SingleOrDefault() is not { } target) return new(Failure: CampaignFailure.NotFound);
            await CampaignPersistence.LockMember(db, campaign, actor, ct);
            await CampaignPersistence.LockCampaign(db, campaign, ct);
            var fresh = await Read(campaign, actor, ct);
            if (fresh is null) return new(Failure: CampaignFailure.NotFound);
            if (!CampaignPolicy.IsOwner(fresh.OwnerId, actor, fresh.Document.Role)) return new(Failure: CampaignFailure.Forbidden);
            if (fresh.OwnerId != before.OwnerId || fresh.Document.Role != before.Document.Role) return CampaignPersistence.Changed<bool>();
            if (target.ExpiresAt <= DateTime.UtcNow || target.UsedAt is not null) return new(false);
            target.ExpiresAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(true);
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<bool>(); }
    }
    public async Task<CampaignResult<CampaignInviteDocument>> Create(CampaignContext before, Guid actor, string role, string token, DateTime expiresAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var campaign = before.Document.Id;
            await CampaignPersistence.LockMember(db, campaign, actor, ct); await CampaignPersistence.LockCampaign(db, campaign, ct);
            var fresh = await Read(campaign, actor, ct);
            if (fresh is null) return new(Failure: CampaignFailure.NotFound);
            if (!CampaignPolicy.IsOwner(fresh.OwnerId, actor, fresh.Document.Role)) return new(Failure: CampaignFailure.Forbidden);
            if (fresh.OwnerId != before.OwnerId || fresh.Document.Role != before.Document.Role) return CampaignPersistence.Changed<CampaignInviteDocument>();
            var invite = new CampaignInvite { CampaignId = campaign, CreatedById = actor, Role = role, TokenHash = CampaignInviteToken.Hash(token), ExpiresAt = expiresAt };
            db.CampaignInvites.Add(invite); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(new(token, role, invite.ExpiresAt, invite.Id));
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<CampaignInviteDocument>(); }
    }
    public Task<CampaignInvitePreview?> Preview(string hash, DateTime now, CancellationToken ct) => db.CampaignInvites.AsNoTracking()
        .Where(i => i.TokenHash == hash && i.UsedAt == null && i.ExpiresAt > now && i.CreatedById == i.Campaign.OwnerId &&
            (i.Role == "gm" || i.Role == "co-gm" || i.Role == "player" || i.Role == "spectator") &&
            i.Campaign.Memberships.Any(m => m.UserId == i.CreatedById && m.Role == "owner"))
        .Select(i => new CampaignInvitePreview(i.CampaignId, i.Campaign.Name, i.Role, i.ExpiresAt)).SingleOrDefaultAsync(ct);

    public async Task<CampaignResult<CampaignJoined>> Accept(string hash, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var matches = await db.CampaignInvites.FromSqlInterpolated($"SELECT * FROM campaign_invites WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync(ct);
            var invite = matches.SingleOrDefault();
            if (invite is null || !CampaignPolicy.AvailableInvite(invite.Role, invite.ExpiresAt, invite.UsedAt, DateTime.UtcNow)) return new(Failure: CampaignFailure.NotFound);
            await CampaignPersistence.LockMember(db, invite.CampaignId, invite.CreatedById, ct);
            // Serialize joins even for distinct tokens: the prospective membership row may not exist yet.
            var campaigns = await db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {invite.CampaignId} FOR UPDATE").ToListAsync(ct);
            var owner = await Read(invite.CampaignId, invite.CreatedById, ct);
            if (campaigns.Count == 0 || owner is null || !CampaignPolicy.IsOwner(owner.OwnerId, invite.CreatedById, owner.Document.Role) ||
                !CampaignPolicy.AvailableInvite(invite.Role, invite.ExpiresAt, invite.UsedAt, DateTime.UtcNow)) return new(Failure: CampaignFailure.NotFound);
            if (await db.CampaignMemberships.AnyAsync(m => m.CampaignId == invite.CampaignId && m.UserId == actor, ct))
                return new(new(invite.CampaignId));
            db.CampaignMemberships.Add(new() { CampaignId = invite.CampaignId, UserId = actor, Role = invite.Role });
            // Available links belong to the campaign and remain usable until expiry.
            // The UsedAt guard keeps previously consumed legacy links closed.
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(new(invite.CampaignId));
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<CampaignJoined>(); }
    }
}
