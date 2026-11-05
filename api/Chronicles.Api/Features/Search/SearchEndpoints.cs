using System.Security.Claims;
using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", async Task<IResult> (string? q, HttpContext http, AppDbContext db) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var term = q?.Trim() ?? "";
            if (term.Length is < 2 or > 120)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Digite entre 2 e 120 caracteres."] });
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

            var campaigns = await db.Campaigns.AsNoTracking()
                .Where(x => x.Memberships.Any(m => m.UserId == userId) &&
                    (EF.Functions.ILike(x.Name, pattern, "\\") || EF.Functions.ILike(x.Description, pattern, "\\")))
                .OrderByDescending(x => x.UpdatedAt).Take(10)
                .Select(x => new { x.Id, x.Name, x.Status, x.UpdatedAt }).ToListAsync();

            var characters = await db.Characters.AsNoTracking()
                .Where(x => x.Campaign.Memberships.Any(m => m.UserId == userId) && EF.Functions.ILike(x.Name, pattern, "\\"))
                .OrderByDescending(x => x.UpdatedAt).Take(10)
                .Select(x => new { x.Id, x.Name, x.CampaignId, campaignName = x.Campaign.Name, x.UpdatedAt }).ToListAsync();

            var npcs = await db.Npcs.AsNoTracking()
                .Where(x => x.Campaign.Memberships.Any(m => m.UserId == userId) &&
                    (x.Visibility == "campaign" || x.Campaign.Memberships.Any(m => m.UserId == userId &&
                        (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm"))) && EF.Functions.ILike(x.Name, pattern, "\\"))
                .OrderByDescending(x => x.UpdatedAt).Take(10)
                .Select(x => new { x.Id, x.Name, x.CampaignId, campaignName = x.Campaign.Name, x.UpdatedAt }).ToListAsync();

            var worlds = await db.Worlds.AsNoTracking()
                .Where(x => (x.OwnerId == userId || db.CampaignMemberships.Any(m => m.UserId == userId && m.Campaign.WorldId == x.Id)) &&
                    (EF.Functions.ILike(x.Name, pattern, "\\") || EF.Functions.ILike(x.Description, pattern, "\\")))
                .OrderByDescending(x => x.UpdatedAt).Take(10)
                .Select(x => new { x.Id, x.Name, x.UpdatedAt }).ToListAsync();

            var articles = await db.WorldArticles.AsNoTracking()
                .Where(x => (x.World.OwnerId == userId || db.CampaignMemberships.Any(m => m.UserId == userId && m.Campaign.WorldId == x.WorldId)) &&
                    (x.Visibility == "campaign" || x.Visibility == "private" && x.AuthorId == userId ||
                     db.CampaignMemberships.Any(m => m.UserId == userId && m.Campaign.WorldId == x.WorldId &&
                         (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm")) || x.World.OwnerId == userId) &&
                    (EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.Body, pattern, "\\")))
                .OrderByDescending(x => x.UpdatedAt).Take(10)
                .Select(x => new { x.Id, x.WorldId, x.Title, x.Category, worldName = x.World.Name, x.UpdatedAt }).ToListAsync();

            var entries = await db.NarrativeEntries.AsNoTracking()
                .Where(x => x.Campaign.Memberships.Any(m => m.UserId == userId) &&
                    (x.Visibility == "campaign" || x.Visibility == "private" && x.AuthorId == userId ||
                     x.Campaign.Memberships.Any(m => m.UserId == userId && (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm"))) &&
                    (EF.Functions.ILike(x.Title, pattern, "\\") || EF.Functions.ILike(x.Body, pattern, "\\")))
                .OrderByDescending(x => x.UpdatedAt).Take(20)
                .Select(x => new { x.Id, x.CampaignId, x.Kind, x.Title, x.Category, campaignName = x.Campaign.Name, x.UpdatedAt }).ToListAsync();

            return Results.Ok(new { campaigns, characters, npcs, worlds, articles, entries });
        });
    }
}
