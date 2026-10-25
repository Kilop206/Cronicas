using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chronicles.Api;

public static class NpcEndpoints
{
    public static void MapNpcEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/npcs", async Task<IResult> (Guid campaignId, int? page, string? search, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            if (search?.Length > 120) return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Busca muito longa."] });
            var currentPage = Math.Clamp(page ?? 1, 1, 100000);
            var query = db.Npcs.AsNoTracking().Where(x => x.CampaignId == campaignId);
            if (!Master(role)) query = query.Where(x => x.Visibility == "campaign");
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
            }
            var total = await query.CountAsync();
            var npcs = await query.OrderBy(x => x.Name).Skip((currentPage - 1) * 20).Take(20).ToListAsync();
            return Results.Ok(new { items = npcs.Select(x => View(x, Master(role))), total, page = currentPage });
        });

        api.MapPost("/campaigns/{campaignId:guid}/npcs", async Task<IResult> (Guid campaignId, NpcInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres criam NPCs.", statusCode: 403);
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var npc = new Npc { CampaignId = campaignId, Name = input.Name!.Trim() };
            Apply(npc, input);
            db.Npcs.Add(npc);
            await db.SaveChangesAsync();
            return Results.Created($"/api/npcs/{npc.Id}", View(npc, true));
        });

        api.MapGet("/npcs/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var npc = await db.Npcs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (npc is null) return Results.NotFound();
            var role = await Role(db, npc.CampaignId, userId);
            if (role is null || !Master(role) && npc.Visibility != "campaign") return Results.NotFound();
            return Results.Ok(View(npc, Master(role)));
        });

        api.MapPut("/npcs/{id:guid}", async Task<IResult> (Guid id, NpcInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var npc = await db.Npcs.SingleOrDefaultAsync(x => x.Id == id);
            if (npc is null) return Results.NotFound();
            var role = await Role(db, npc.CampaignId, userId);
            if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres editam NPCs.", statusCode: 403);
            if (input.Version != npc.Version) return Results.Conflict(new { message = "O NPC mudou. Atualize antes de salvar." });
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            Apply(npc, input);
            npc.Version++;
            npc.UpdatedAt = DateTime.UtcNow;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O NPC mudou. Atualize antes de salvar." }); }
            return Results.Ok(View(npc, true));
        });

        api.MapDelete("/npcs/{id:guid}", async Task<IResult> (Guid id, int? version, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var npc = await db.Npcs.SingleOrDefaultAsync(x => x.Id == id);
            if (npc is null) return Results.NotFound();
            var role = await Role(db, npc.CampaignId, userId);
            if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres excluem NPCs.", statusCode: 403);
            if (version != npc.Version) return Results.Conflict(new { message = "O NPC mudou. Atualize antes de excluir." });
            if (await db.EncounterParticipants.AnyAsync(x => x.NpcId == id))
                return Results.Conflict(new { message = "Remova o NPC dos encontros antes de excluir." });
            db.Npcs.Remove(npc);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O NPC mudou. Atualize antes de excluir." }); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            { return Results.Conflict(new { message = "Remova o NPC dos encontros antes de excluir." }); }
            return Results.NoContent();
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Task<string?> Role(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AsNoTracking()
        .Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
    private static bool Master(string role) => role is "owner" or "gm" or "co-gm";
    private static object View(Npc npc, bool master)
    {
        var result = new Dictionary<string, object?>
        {
            ["id"] = npc.Id, ["campaignId"] = npc.CampaignId, ["name"] = npc.Name, ["portraitUrl"] = npc.PortraitUrl,
            ["appearance"] = npc.Appearance, ["personality"] = npc.Personality, ["motivation"] = npc.Motivation,
            ["relationships"] = npc.Relationships, ["location"] = npc.Location, ["status"] = npc.Status,
            ["visibility"] = npc.Visibility, ["health"] = npc.Health, ["maxHealth"] = npc.MaxHealth,
            ["version"] = npc.Version, ["createdAt"] = npc.CreatedAt, ["updatedAt"] = npc.UpdatedAt
        };
        if (master) result["secrets"] = npc.Secrets;
        return result;
    }
    private static void Apply(Npc npc, NpcInput input)
    {
        npc.Name = input.Name!.Trim();
        npc.PortraitUrl = string.IsNullOrWhiteSpace(input.PortraitUrl) ? null : input.PortraitUrl.Trim();
        npc.Appearance = input.Appearance!.Trim();
        npc.Personality = input.Personality!.Trim();
        npc.Motivation = input.Motivation!.Trim();
        npc.Relationships = input.Relationships!.Trim();
        npc.Location = input.Location!.Trim();
        npc.Secrets = input.Secrets!.Trim();
        npc.Status = input.Status!;
        npc.Visibility = input.Visibility!;
        npc.Health = input.Health;
        npc.MaxHealth = input.MaxHealth;
    }
    private static Dictionary<string, string[]> Validate(NpcInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 2 and <= 120)) errors["name"] = ["Nome deve ter entre 2 e 120 caracteres."];
        if (input.PortraitUrl is { Length: > 0 } url && (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps))
            errors["portraitUrl"] = ["Use uma URL HTTPS de até 2048 caracteres."];
        if (input.Appearance is null || input.Appearance.Length > 2000 || input.Personality is null || input.Personality.Length > 2000 ||
            input.Motivation is null || input.Motivation.Length > 2000 || input.Relationships is null || input.Relationships.Length > 2000 ||
            input.Location is null || input.Location.Length > 160 || input.Secrets is null || input.Secrets.Length > 10000)
            errors["details"] = ["Um ou mais campos excedem o limite permitido."];
        if (input.Status is not ("active" or "missing" or "deceased" or "unknown")) errors["status"] = ["Status inválido."];
        if (input.Visibility is not ("gm" or "campaign")) errors["visibility"] = ["Visibilidade inválida."];
        if (input.MaxHealth is < 1 or > 9999 || input.Health < 0 || input.Health > input.MaxHealth) errors["health"] = ["Vida fora do limite."];
        return errors;
    }
    public sealed record NpcInput(string? Name, string? PortraitUrl, string? Appearance, string? Personality,
        string? Motivation, string? Relationships, string? Location, string? Secrets, string? Status,
        string? Visibility, int Health, int MaxHealth, int Version);
}
