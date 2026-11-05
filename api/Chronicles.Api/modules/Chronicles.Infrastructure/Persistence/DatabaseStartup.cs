using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Persistence;

public static class DatabaseStartup
{
    public static async Task EnsureReady(AppDbContext db, bool allowDevelopmentMigrations, CancellationToken ct = default)
    {
        if (allowDevelopmentMigrations) await db.Database.MigrateAsync(ct);
        else if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Database migrations are pending. Apply the reviewed release migration before starting this version.");
    }
}
