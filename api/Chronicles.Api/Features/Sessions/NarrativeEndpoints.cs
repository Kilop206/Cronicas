using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class NarrativeEndpoints
{
    private static readonly string[] Kinds = ["sessions", "documents", "wiki"];
    private static readonly string[] Visibility = ["campaign", "private", "gm"];

    public static void MapNarrativeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/entries/{kind}", async Task<IResult> (Guid campaignId, string kind, int? page, string? search, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!Kinds.Contains(kind)) return Results.NotFound();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            var currentPage = Math.Clamp(page ?? 1, 1, 100000);
            var query = Visible(db.NarrativeEntries.AsNoTracking().Where(x => x.CampaignId == campaignId && x.Kind == kind), userId, role);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                if (term.Length > 120) return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Busca muito longa."] });
                query = query.Where(x => EF.Functions.ILike(x.Title, $"%{term}%") || EF.Functions.ILike(x.Body, $"%{term}%"));
            }
            var total = await query.CountAsync();
            var entries = await query.OrderByDescending(x => x.EventAt ?? x.UpdatedAt).ThenByDescending(x => x.UpdatedAt)
                .Skip((currentPage - 1) * 20).Take(20).ToListAsync();
            return Results.Ok(new { items = entries.Select(View), total, page = currentPage });
        });

        api.MapPost("/campaigns/{campaignId:guid}/entries/{kind}", async Task<IResult> (Guid campaignId, string kind, EntryInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!Kinds.Contains(kind)) return Results.NotFound();
            var role = await Role(db, campaignId, userId);
            if (role is null) return Results.NotFound();
            if (role == "spectator" || kind == "sessions" && !Master(role) || input.Visibility == "gm" && !Master(role)) return Results.Problem("Sem permissão para criar este conteúdo.", statusCode: 403);
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var entry = new NarrativeEntry { CampaignId = campaignId, AuthorId = userId, Kind = kind, Title = input.Title.Trim() };
            Apply(entry, input);
            db.NarrativeEntries.Add(entry);
            await db.SaveChangesAsync();
            return Results.Created($"/api/entries/{entry.Id}", View(entry));
        });

        api.MapGet("/entries/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var entry = await db.NarrativeEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (entry is null) return Results.NotFound();
            var role = await Role(db, entry.CampaignId, userId);
            return role is null || !CanSee(entry, userId, role) ? Results.NotFound() : Results.Ok(View(entry));
        });

        api.MapPut("/entries/{id:guid}", async Task<IResult> (Guid id, EntryInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var entry = await db.NarrativeEntries.SingleOrDefaultAsync(x => x.Id == id);
            if (entry is null) return Results.NotFound();
            var role = await Role(db, entry.CampaignId, userId);
            if (role is null || !CanSee(entry, userId, role)) return Results.NotFound();
            if (!CanEdit(entry, userId, role) || input.Visibility == "gm" && !Master(role)) return Results.Problem("Sem permissão para editar este conteúdo.", statusCode: 403);
            if (input.Revision != entry.Revision) return Results.Conflict(new { message = "Este conteúdo mudou. Atualize a página antes de salvar." });
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            Apply(entry, input);
            entry.Revision++;
            await db.SaveChangesAsync();
            return Results.Ok(View(entry));
        });

        api.MapDelete("/entries/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var entry = await db.NarrativeEntries.SingleOrDefaultAsync(x => x.Id == id);
            if (entry is null) return Results.NotFound();
            var role = await Role(db, entry.CampaignId, userId);
            if (role is null || !CanSee(entry, userId, role)) return Results.NotFound();
            if (!CanEdit(entry, userId, role)) return Results.Problem("Sem permissão para excluir este conteúdo.", statusCode: 403);
            db.NarrativeEntries.Remove(entry);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapPost("/entries/{id:guid}/reveal", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var entry = await db.NarrativeEntries.SingleOrDefaultAsync(x => x.Id == id);
            if (entry is null) return Results.NotFound();
            var role = await Role(db, entry.CampaignId, userId);
            if (role is null) return Results.NotFound();
            if (!Master(role)) return Results.Problem("Apenas mestres podem revelar conteúdo.", statusCode: 403);
            entry.Visibility = "campaign";
            entry.RevealedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;
            entry.Revision++;
            await db.SaveChangesAsync();
            return Results.Ok(View(entry));
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Task<string?> Role(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
    private static bool Master(string role) => role is "owner" or "gm" or "co-gm";
    private static bool CanSee(NarrativeEntry entry, Guid userId, string role) => entry.Visibility == "campaign" || entry.Visibility == "private" && (entry.AuthorId == userId || Master(role)) || entry.Visibility == "gm" && Master(role);
    private static bool CanEdit(NarrativeEntry entry, Guid userId, string role) => Master(role) || entry.AuthorId == userId && entry.Kind != "sessions" && role != "spectator";
    private static IQueryable<NarrativeEntry> Visible(IQueryable<NarrativeEntry> query, Guid userId, string role) => Master(role)
        ? query : query.Where(x => x.Visibility == "campaign" || x.Visibility == "private" && x.AuthorId == userId);
    private static object View(NarrativeEntry x) => new { x.Id, x.CampaignId, x.AuthorId, x.Kind, x.Title, x.Body, x.Category, x.Visibility, x.EventAt, x.RevealedAt, x.CreatedAt, x.UpdatedAt, x.Revision };
    private static Dictionary<string, string[]> Validate(EntryInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Title?.Trim().Length is not (>= 2 and <= 160)) errors["title"] = ["Título deve ter entre 2 e 160 caracteres."];
        if (input.Body is null || input.Body.Length > 40000) errors["body"] = ["Conteúdo deve ter até 40.000 caracteres."];
        if (input.Category?.Length is not (>= 1 and <= 40) || !input.Category.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')) errors["category"] = ["Categoria inválida."];
        if (!Visibility.Contains(input.Visibility)) errors["visibility"] = ["Visibilidade inválida."];
        if (input.EventAt?.Year is < 1 or > 9998) errors["eventAt"] = ["Data inválida."];
        return errors;
    }
    private static void Apply(NarrativeEntry entry, EntryInput input)
    {
        entry.Title = input.Title.Trim();
        entry.Body = input.Body.Trim();
        entry.Category = input.Category.Trim().ToLowerInvariant();
        entry.Visibility = input.Visibility;
        entry.EventAt = input.EventAt?.ToUniversalTime();
        entry.UpdatedAt = DateTime.UtcNow;
        if (entry.Visibility == "campaign" && entry.RevealedAt is null) entry.RevealedAt = DateTime.UtcNow;
    }
    public sealed record EntryInput(string Title, string Body, string Category, string Visibility, DateTime? EventAt, int Revision);
}
