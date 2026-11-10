using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.Campaigns;
using Chronicles.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Campaigns;

public sealed class EfCampaignStore(AppDbContext db) : ICampaignStore, ICampaignMembershipStore
{
    public Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct) => CampaignPersistence.Read(db, campaign, actor, ct);
    public async Task<CampaignPage> List(Guid actor, int page, string? search, CancellationToken ct)
    {
        var query = db.CampaignMemberships.AsNoTracking().Where(m => m.UserId == actor);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(m => EF.Functions.ILike(m.Campaign.Name, $"%{search}%"));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.Campaign.UpdatedAt).ThenBy(m => m.CampaignId).Skip((page - 1) * 20).Take(20)
            .Select(m => new CampaignDocument(m.CampaignId, m.Campaign.Name, m.Campaign.Description, m.Campaign.Status,
                m.Campaign.AccentColor, m.Role, m.Campaign.UpdatedAt, m.Campaign.SystemId, m.Campaign.WorldId)).ToArrayAsync(ct);
        return new(items, total, page);
    }
    public async Task<CampaignDocument> Create(Guid actor, CampaignDetails details, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        var campaign = new Campaign { OwnerId = actor, Name = details.Name, Description = details.Description, AccentColor = details.AccentColor };
        campaign.Memberships.Add(new() { UserId = actor, Role = "owner" });
        db.Campaigns.Add(campaign); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Document(campaign, "owner");
    }
    public async Task<CampaignResult<CampaignDocument>> Save(CampaignContext before, Guid actor, CampaignDetails? details, string? status, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var id = before.Document.Id;
            await CampaignPersistence.LockMember(db, id, actor, ct);
            var rows = await db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct);
            var fresh = await Read(id, actor, ct);
            if (fresh is null) return new(Failure: CampaignFailure.NotFound);
            if (!CampaignPolicy.IsOwner(fresh.OwnerId, actor, fresh.Document.Role)) return new(Failure: CampaignFailure.Forbidden);
            if (fresh != before) return CampaignPersistence.Changed<CampaignDocument>();
            var campaign = rows.Single();
            if (details is not null) { campaign.Name = details.Name; campaign.Description = details.Description; campaign.AccentColor = details.AccentColor; }
            if (status is not null) campaign.Status = status;
            campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(Document(campaign, fresh.Document.Role));
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<CampaignDocument>(); }
    }
    public async Task<CampaignMemberDocument[]?> Members(Guid campaign, Guid actor, CancellationToken ct)
    {
        if (await Read(campaign, actor, ct) is null) return null;
        return await db.CampaignMemberships.AsNoTracking().Where(m => m.CampaignId == campaign && db.CampaignMemberships.Any(access => access.CampaignId == campaign && access.UserId == actor))
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId).Select(m => new CampaignMemberDocument(m.UserId, m.User.DisplayName, m.Role, m.JoinedAt)).ToArrayAsync(ct);
    }
    public Task<CampaignMemberDocument?> Member(Guid campaign, Guid target, CancellationToken ct) => db.CampaignMemberships.AsNoTracking()
        .Where(m => m.CampaignId == campaign && m.UserId == target).Select(m => new CampaignMemberDocument(m.UserId, m.User.DisplayName, m.Role, m.JoinedAt)).SingleOrDefaultAsync(ct);
    public async Task<CampaignResult<bool>> Change(CampaignContext before, CampaignMemberDocument target, Guid actor, string? role, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var id = before.Document.Id;
            var rows = await db.CampaignMemberships.FromSqlInterpolated($"SELECT * FROM campaign_memberships WHERE \"CampaignId\" = {id} AND (\"UserId\" = {actor} OR \"UserId\" = {target.UserId}) ORDER BY \"UserId\" FOR UPDATE").ToListAsync(ct);
            await CampaignPersistence.LockCampaign(db, id, ct);
            var fresh = await Read(id, actor, ct);
            if (fresh is null) return new(Failure: CampaignFailure.NotFound);
            if (role is not null && !CampaignPolicy.IsOwner(fresh.OwnerId, actor, fresh.Document.Role)) return new(Failure: CampaignFailure.NotFound);
            if (role is null && !CampaignPolicy.MayRemove(fresh.OwnerId, actor, target.UserId)) return new(Failure: CampaignFailure.Forbidden);
            if (role is not null && (!CampaignPolicy.InvitableRole(role) || target.UserId == fresh.OwnerId || target.UserId == actor))
                return new(Failure: CampaignFailure.Invalid, Errors: new() { ["role"] = ["Papel inválido."] });
            var member = rows.SingleOrDefault(m => m.UserId == target.UserId);
            if (member is null) return new(Failure: CampaignFailure.NotFound);
            if (fresh.OwnerId != before.OwnerId || fresh.Document.Role != before.Document.Role || member.Role != target.Role || member.JoinedAt != target.JoinedAt)
                return CampaignPersistence.Changed<bool>();
            if (role == member.Role) return new(false);
            if (role is null) db.CampaignMemberships.Remove(member); else member.Role = role;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(true);
        }
        catch (Exception error) when (CampaignPersistence.Conflict(error)) { db.ChangeTracker.Clear(); return CampaignPersistence.Changed<bool>(); }
    }
    private static CampaignDocument Document(Campaign c, string role) => new(c.Id, c.Name, c.Description, c.Status, c.AccentColor, role, c.UpdatedAt, c.SystemId, c.WorldId);
}
