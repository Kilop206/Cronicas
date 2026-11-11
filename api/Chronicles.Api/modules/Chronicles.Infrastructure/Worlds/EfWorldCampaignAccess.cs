using Chronicles.Api.Data;
using Chronicles.Application.Worlds;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Worlds;

public sealed class EfWorldCampaignAccess(AppDbContext db) : IWorldCampaignAccess
{
    public async Task<bool> HoldOwnedReference(Guid world, Guid actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("World references require an active local transaction.");
        var rows = await db.Worlds.FromSqlInterpolated($"SELECT * FROM worlds WHERE \"Id\" = {world} AND \"OwnerId\" = {actor} FOR SHARE").AsNoTracking().ToListAsync(ct);
        return rows.Count == 1;
    }
}
