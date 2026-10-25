using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chronicles.Api;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/messages", async Task<IResult> (Guid campaignId, long? after, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId)) return Results.NotFound();
            var query = db.ChatMessages.AsNoTracking().Where(x => x.CampaignId == campaignId);
            if (after is { } unix && unix > 0 && unix < DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            {
                var since = DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime;
                query = query.Where(x => x.CreatedAt > since);
            }
            var messages = await query.OrderByDescending(x => x.CreatedAt).Take(100).Include(x => x.User).ToListAsync();
            return Results.Ok(messages.OrderBy(x => x.CreatedAt).Select(View));
        });

        api.MapPost("/campaigns/{campaignId:guid}/messages", async Task<IResult> (Guid campaignId, MessageInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null) return Results.NotFound();
            if (role == "spectator" || input.Kind == "announcement" && role is not ("owner" or "gm" or "co-gm")) return Results.Problem("Sem permissão para enviar esta mensagem.", statusCode: 403);
            if (input.ClientId == Guid.Empty || input.Kind is not ("chat" or "announcement") || input.Body?.Trim().Length is not (>= 1 and <= 2000))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["Mensagem, tipo ou ID inválido."] });
            var existing = await db.ChatMessages.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == userId && x.ClientId == input.ClientId);
            if (existing is not null) return Results.Ok(View(existing));
            var message = new ChatMessage { CampaignId = campaignId, UserId = userId, ClientId = input.ClientId, Body = input.Body.Trim(), Kind = input.Kind };
            db.ChatMessages.Add(message);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(message).State = EntityState.Detached;
                var duplicate = await db.ChatMessages.AsNoTracking().Include(x => x.User).SingleAsync(x => x.CampaignId == campaignId && x.UserId == userId && x.ClientId == input.ClientId);
                return Results.Ok(View(duplicate));
            }
            message.User = await db.Users.SingleAsync(x => x.Id == userId);
            return Results.Created($"/api/campaigns/{campaignId}/messages", View(message));
        }).RequireRateLimiting("rolls");
    }
    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static object View(ChatMessage x) => new { x.Id, x.CampaignId, x.UserId, userName = x.User.DisplayName, x.Body, x.Kind, x.CreatedAt };
    public sealed record MessageInput(Guid ClientId, string Body, string Kind);
}
