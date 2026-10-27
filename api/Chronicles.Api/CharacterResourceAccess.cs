using System.Security.Claims;
using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

internal static class CharacterResourceAccess
{
    internal static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    internal static Task<bool> CanRead(AppDbContext db, Guid characterId, Guid userId) =>
        db.Characters.AnyAsync(x => x.Id == characterId && x.Campaign.Memberships.Any(m => m.UserId == userId));

    // Null means the campaign or character is inaccessible; false means a member may read but not edit.
    internal static async Task<bool?> CanEdit(AppDbContext db, Guid characterId, Guid userId)
    {
        var member = await db.Characters.AsNoTracking()
            .Where(x => x.Id == characterId && x.Campaign.Memberships.Any(m => m.UserId == userId))
            .Select(x => new { x.OwnerId, Role = x.Campaign.Memberships.Where(m => m.UserId == userId).Select(m => m.Role).First() })
            .SingleOrDefaultAsync();
        return member is null ? null : member.Role != "spectator" && (member.OwnerId == userId || member.Role is "owner" or "gm" or "co-gm");
    }
}
