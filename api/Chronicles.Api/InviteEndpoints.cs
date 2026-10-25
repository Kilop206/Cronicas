using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class InviteEndpoints
{
    public static void MapInviteEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/campaigns/{id:guid}/invites", async Task<IResult> (Guid id, InviteRequest input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var member = await db.CampaignMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.CampaignId == id && x.UserId == userId);
            if (member is null) return Results.NotFound();
            if (member.Role != "owner") return Results.Problem("Apenas o proprietário pode convidar participantes.", statusCode: 403);
            if (input.Role is not ("gm" or "co-gm" or "player" or "spectator"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Papel de convite inválido."] });
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var invite = new CampaignInvite { CampaignId = id, CreatedById = userId, Role = input.Role, TokenHash = Hash(token) };
            db.CampaignInvites.Add(invite);
            await db.SaveChangesAsync();
            http.Response.Headers.CacheControl = "no-store";
            return Results.Created($"/api/invites/{token}", new { token, invite.Role, invite.ExpiresAt });
        }).RequireRateLimiting("auth");

        api.MapGet("/invites/{token}", async Task<IResult> (string token, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is null) return Results.Unauthorized();
            if (!ValidToken(token)) return Results.NotFound();
            var hash = Hash(token);
            var invite = await db.CampaignInvites.AsNoTracking().Where(x => x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .Select(x => new { x.CampaignId, CampaignName = x.Campaign.Name, x.Role, x.ExpiresAt }).SingleOrDefaultAsync();
            return invite is null ? Results.NotFound() : Results.Ok(invite);
        });

        api.MapPost("/invites/{token}/accept", async Task<IResult> (string token, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!ValidToken(token)) return Results.NotFound();
            var hash = Hash(token);
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
            var matches = await db.CampaignInvites.FromSqlInterpolated($"SELECT * FROM campaign_invites WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync(http.RequestAborted);
            var invite = matches.SingleOrDefault();
            if (invite is null || invite.UsedAt is not null || invite.ExpiresAt <= DateTime.UtcNow) return Results.NotFound();
            if (await db.CampaignMemberships.AnyAsync(x => x.CampaignId == invite.CampaignId && x.UserId == userId, http.RequestAborted))
                return Results.Conflict(new { message = "Você já participa desta campanha." });
            db.CampaignMemberships.Add(new CampaignMembership { CampaignId = invite.CampaignId, UserId = userId, Role = invite.Role });
            invite.UsedAt = DateTime.UtcNow;
            invite.UsedById = userId;
            await db.SaveChangesAsync(http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
            return Results.Ok(new { invite.CampaignId });
        }).RequireRateLimiting("auth");
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static bool ValidToken(string token) => token.Length == 48 && token.All(Uri.IsHexDigit);
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()))).ToLowerInvariant();
    public sealed record InviteRequest(string Role);
}
