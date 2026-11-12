using Chronicles.Api.Data;
using Chronicles.Application.RpgSystems;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.RpgSystems;

public sealed class EfRpgSystemCampaignAccess(AppDbContext db) : IRpgSystemCampaignAccess
{
    public async Task<bool> HoldOwnedReference(Guid system, Guid actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Registry references require an active local transaction.");
        var rows = await db.RpgSystems.FromSqlInterpolated($"SELECT * FROM rpg_systems WHERE \"Id\" = {system} AND \"OwnerId\" = {actor} FOR SHARE").AsNoTracking().ToListAsync(ct);
        return rows.Count == 1;
    }
}
