using Chronicles.Api.Data;
using Chronicles.Application.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Campaigns;

internal static class CampaignPersistence
{
    internal static Task<CampaignContext?> Read(AppDbContext db, Guid campaign, Guid actor, CancellationToken ct) =>
        db.CampaignMemberships.AsNoTracking().Where(m => m.CampaignId == campaign && m.UserId == actor)
            .Select(m => new CampaignContext(new CampaignDocument(m.CampaignId, m.Campaign.Name, m.Campaign.Description,
                m.Campaign.Status, m.Campaign.AccentColor, m.Role, m.Campaign.UpdatedAt, m.Campaign.SystemId, m.Campaign.WorldId), m.Campaign.OwnerId))
            .SingleOrDefaultAsync(ct);

    // Membership rows precede campaign rows, matching character writes. Stable ordering prevents crossed member locks.
    internal static Task LockMember(AppDbContext db, Guid campaign, Guid actor, CancellationToken ct) =>
        db.CampaignMemberships.FromSqlInterpolated($"SELECT * FROM campaign_memberships WHERE \"CampaignId\" = {campaign} AND \"UserId\" = {actor} FOR SHARE").ToListAsync(ct);
    internal static Task LockCampaign(AppDbContext db, Guid campaign, CancellationToken ct) =>
        db.Campaigns.FromSqlInterpolated($"SELECT * FROM campaigns WHERE \"Id\" = {campaign} FOR SHARE").ToListAsync(ct);
    internal static bool Conflict(Exception error)
    {
        if (error is DbUpdateConcurrencyException) return true;
        var pg = error as Npgsql.PostgresException ?? (error as DbUpdateException)?.InnerException as Npgsql.PostgresException;
        return pg?.SqlState is "40001" or "40P01" || pg is { SqlState: "23505", ConstraintName: "PK_campaign_memberships" };
    }
    internal static CampaignResult<T> Changed<T>() => new(Failure: CampaignFailure.Conflict, Message: "A campanha ou a participação mudou. Recarregue antes de continuar.");
}
