using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chronicles.Api;

public static class DiceEndpoints
{
    public static void MapDiceEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/rolls", async Task<IResult> (Guid campaignId, long? after, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            var query = db.DiceRolls.AsNoTracking().Where(x => x.CampaignId == campaignId);
            if (after is { } unix && unix > 0 && unix < DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())
            {
                var since = DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime;
                query = query.Where(x => x.CreatedAt > since);
            }
            if (!Master(role)) query = query.Where(x => x.Visibility == "campaign" || x.Visibility == "private" && x.UserId == userId);
            var rolls = await query.OrderByDescending(x => x.CreatedAt).Take(50).Include(x => x.User).ToListAsync();
            return Results.Ok(rolls.OrderBy(x => x.CreatedAt).Select(View));
        });

        api.MapPost("/campaigns/{campaignId:guid}/rolls", async Task<IResult> (Guid campaignId, RollInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            if (role == "spectator" || input.Visibility == "gm" && !Master(role)) return Results.Problem("Sem permissão para esta rolagem.", statusCode: 403);
            if (input.RequestId == Guid.Empty || input.Visibility is not ("campaign" or "private" or "gm") || !DiceExpression.TryParse(input.Expression, out var dice))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["roll"] = ["Use uma expressão como 2d6+3 ou 2d20kh1; informe visibilidade e ID válido."] });
            var existing = await db.DiceRolls.Include(x => x.User).AsNoTracking().SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == userId && x.RequestId == input.RequestId);
            if (existing is not null) return Results.Ok(View(existing));
            var result = dice!.Roll(sides => RandomNumberGenerator.GetInt32(1, sides + 1));
            var roll = new DiceRoll { CampaignId = campaignId, UserId = userId, RequestId = input.RequestId, Expression = input.Expression.Trim().ToLowerInvariant(), RollsJson = JsonSerializer.Serialize(result.Rolls), KeptIndexesJson = JsonSerializer.Serialize(result.KeptIndexes), Modifier = dice.Modifier, Total = result.Total, Visibility = input.Visibility };
            db.DiceRolls.Add(roll);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.Entry(roll).State = EntityState.Detached;
                var duplicate = await db.DiceRolls.Include(x => x.User).AsNoTracking().SingleAsync(x => x.CampaignId == campaignId && x.UserId == userId && x.RequestId == input.RequestId);
                return Results.Ok(View(duplicate));
            }
            roll.User = await db.Users.SingleAsync(x => x.Id == userId);
            return Results.Created($"/api/campaigns/{campaignId}/rolls", View(roll));
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Task<string?> Role(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
    private static bool Master(string role) => role is "owner" or "gm" or "co-gm";
    private static object View(DiceRoll roll) => new { roll.Id, roll.CampaignId, roll.UserId, userName = roll.User.DisplayName, roll.Expression, rolls = JsonSerializer.Deserialize<int[]>(roll.RollsJson), keptIndexes = JsonSerializer.Deserialize<int[]>(roll.KeptIndexesJson), roll.Modifier, roll.Total, roll.Visibility, roll.CreatedAt };
    public sealed record RollInput(Guid RequestId, string Expression, string Visibility);
}
