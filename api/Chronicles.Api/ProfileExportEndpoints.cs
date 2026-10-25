using System.Security.Claims;
using System.Text.Json;
using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class ProfileExportEndpoints
{
    public static void MapProfileExportEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/profile/export", async Task<IResult> (HttpContext http, AppDbContext db) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var profile = await db.Users.AsNoTracking().Where(x => x.Id == userId)
                .Select(x => new { x.Id, x.Email, x.DisplayName, x.Bio, x.Accent, x.CreatedAt }).SingleOrDefaultAsync();
            if (profile is null) return Results.Unauthorized();

            var memberships = await db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.CampaignId, campaignName = x.Campaign.Name, x.Role, x.JoinedAt }).ToListAsync();
            var invites = await db.CampaignInvites.AsNoTracking().Where(x => x.CreatedById == userId)
                .Select(x => new { x.Id, x.CampaignId, x.Role, x.CreatedAt, x.ExpiresAt, x.UsedAt }).ToListAsync();
            var campaigns = await db.Campaigns.AsNoTracking().Where(x => x.OwnerId == userId)
                .Select(x => new { x.Id, x.Name, x.Description, x.Status, x.AccentColor, x.WorldId, x.SystemId, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            var characters = await db.Characters.AsNoTracking().Where(x => x.OwnerId == userId)
                .Select(x => new { x.Id, x.CampaignId, x.Name, x.PortraitUrl, x.BackgroundUrl, x.Health, x.MaxHealth,
                    x.Sanity, x.MaxSanity, x.Mana, x.MaxMana, x.FieldsJson, x.Version, x.UpdatedAt }).ToListAsync();
            var characterIds = characters.Select(x => x.Id).ToArray();
            var revisions = await db.CharacterRevisions.AsNoTracking().Where(x => characterIds.Contains(x.CharacterId))
                .Select(x => new { x.Id, x.CharacterId, x.Version, x.SnapshotJson, x.CreatedAt }).ToListAsync();
            var worlds = await db.Worlds.AsNoTracking().Where(x => x.OwnerId == userId)
                .Select(x => new { x.Id, x.Name, x.Description, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            var articles = await db.WorldArticles.AsNoTracking().Where(x => x.AuthorId == userId)
                .Select(x => new { x.Id, x.WorldId, x.Title, x.Body, x.Category, x.Visibility, x.Revision, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            var entries = await db.NarrativeEntries.AsNoTracking().Where(x => x.AuthorId == userId)
                .Select(x => new { x.Id, x.CampaignId, x.Kind, x.Title, x.Body, x.Category, x.Visibility,
                    x.EventAt, x.RevealedAt, x.Revision, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            var systems = await db.RpgSystems.AsNoTracking().Where(x => x.OwnerId == userId)
                .Select(x => new { x.Id, x.Name, x.Description, x.SchemaJson, x.CreatedAt, x.UpdatedAt }).ToListAsync();
            var rolls = await db.DiceRolls.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.Id, x.CampaignId, x.Expression, x.RollsJson, x.KeptIndexesJson,
                    x.Modifier, x.Total, x.Visibility, x.CreatedAt }).ToListAsync();
            var messages = await db.ChatMessages.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.Id, x.CampaignId, x.Body, x.Kind, x.CreatedAt }).ToListAsync();

            var export = new { exportedAt = DateTime.UtcNow, profile, memberships, invites, campaigns, characters, revisions,
                worlds, articles, entries, systems, rolls, messages };
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                "application/json", $"cronicas-dados-{DateTime.UtcNow:yyyyMMdd}.json");
        });
    }
}
