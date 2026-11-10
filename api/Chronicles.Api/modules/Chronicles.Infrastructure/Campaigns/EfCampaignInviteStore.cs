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
            return new(new(token, role, invite.ExpiresAt));
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
                return new(Failure: CampaignFailure.Conflict, Message: "Você já participa desta campanha.");
            db.CampaignMemberships.Add(new() { CampaignId = invite.CampaignId, UserId = actor, Role = invite.Role });
            invite.UsedAt = DateTime.UtcNow; invite.UsedById = actor;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(new(invite.CampaignId));
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<CampaignJoined>(); }
    }
}
