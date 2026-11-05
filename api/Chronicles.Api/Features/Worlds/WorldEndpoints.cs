using System.Security.Claims;
using System.Text.RegularExpressions;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class WorldEndpoints
{
    private static readonly Regex CategoryPattern = new("^[a-z][a-z0-9_-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void MapWorldEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/worlds", async Task<IResult> (HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var worlds = await db.Worlds.AsNoTracking().Where(x => x.OwnerId == userId || db.CampaignMemberships.Any(m => m.UserId == userId && m.Campaign.WorldId == x.Id))
                .OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync();
            return Results.Ok(worlds.Select(View));
        });

        api.MapPost("/worlds", async Task<IResult> (WorldInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var errors = ValidateWorld(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var world = new World { OwnerId = userId, Name = input.Name.Trim(), Description = input.Description.Trim() };
            db.Worlds.Add(world); await db.SaveChangesAsync();
            return Results.Created($"/api/worlds/{world.Id}", View(world));
        });

        api.MapGet("/worlds/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var world = await db.Worlds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (world is null || !(await Access(db, id, userId)).Member) return Results.NotFound();
            var campaigns = await db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == userId && x.Campaign.WorldId == id)
                .Select(x => new { x.CampaignId, x.Campaign.Name, x.Role }).ToListAsync();
            return Results.Ok(new { world.Id, world.OwnerId, world.Name, world.Description, world.CreatedAt, world.UpdatedAt, campaigns });
        });

        api.MapPut("/worlds/{id:guid}", async Task<IResult> (Guid id, WorldInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var world = await db.Worlds.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (world is null) return Results.NotFound();
            var errors = ValidateWorld(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            world.Name = input.Name.Trim(); world.Description = input.Description.Trim(); world.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); return Results.Ok(View(world));
        });

        api.MapDelete("/worlds/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var world = await db.Worlds.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (world is null) return Results.NotFound();
            if (await db.Campaigns.AnyAsync(x => x.WorldId == id)) return Results.Conflict(new { message = "Desassocie as campanhas antes de excluir o universo." });
            db.Worlds.Remove(world); await db.SaveChangesAsync(); return Results.NoContent();
        });

        api.MapPatch("/campaigns/{campaignId:guid}/world", async Task<IResult> (Guid campaignId, WorldLinkInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == userId);
            if (campaign is null) return Results.NotFound();
            if (input.WorldId is { } worldId && !await db.Worlds.AnyAsync(x => x.Id == worldId && x.OwnerId == userId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["worldId"] = ["Universo indisponível."] });
            campaign.WorldId = input.WorldId; campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        api.MapGet("/worlds/{worldId:guid}/articles", async Task<IResult> (Guid worldId, int? page, string? search, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await Access(db, worldId, userId);
            if (!access.Member) return Results.NotFound();
            var currentPage = Math.Clamp(page ?? 1, 1, 100000);
            var query = Visible(db.WorldArticles.AsNoTracking().Where(x => x.WorldId == worldId), access, userId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (search.Length > 120) return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Busca muito longa."] });
                query = query.Where(x => EF.Functions.ILike(x.Title, $"%{search.Trim()}%") || EF.Functions.ILike(x.Body, $"%{search.Trim()}%"));
            }
            var total = await query.CountAsync();
            var articles = await query.OrderByDescending(x => x.UpdatedAt).Skip((currentPage - 1) * 20).Take(20).ToListAsync();
            return Results.Ok(new { items = articles.Select(ArticleView), total, page = currentPage });
        });

        api.MapPost("/worlds/{worldId:guid}/articles", async Task<IResult> (Guid worldId, ArticleInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await Access(db, worldId, userId);
            if (!access.Member) return Results.NotFound();
            if (!access.Write || input.Visibility == "gm" && !access.Master) return Results.Problem("Sem permissão para criar artigo.", statusCode: 403);
            var errors = ValidateArticle(input); if (errors.Count > 0) return Results.ValidationProblem(errors);
            var article = new WorldArticle { WorldId = worldId, AuthorId = userId, Title = input.Title.Trim(), Body = input.Body.Trim(), Category = input.Category.Trim().ToLowerInvariant(), Visibility = input.Visibility };
            db.WorldArticles.Add(article); await db.SaveChangesAsync();
            return Results.Created($"/api/world-articles/{article.Id}", ArticleView(article));
        });

        api.MapGet("/world-articles/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var article = await db.WorldArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (article is null) return Results.NotFound();
            var access = await Access(db, article.WorldId, userId);
            return access.Member && CanSee(article, access, userId) ? Results.Ok(ArticleView(article)) : Results.NotFound();
        });

        api.MapPut("/world-articles/{id:guid}", async Task<IResult> (Guid id, ArticleInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var article = await db.WorldArticles.SingleOrDefaultAsync(x => x.Id == id);
            if (article is null) return Results.NotFound();
            var access = await Access(db, article.WorldId, userId);
            if (!access.Member || !CanSee(article, access, userId)) return Results.NotFound();
            if (!CanEdit(article, access, userId) || input.Visibility == "gm" && !access.Master) return Results.Problem("Sem permissão para editar artigo.", statusCode: 403);
            if (input.Revision != article.Revision) return Results.Conflict(new { message = "O artigo mudou. Atualize antes de salvar." });
            var errors = ValidateArticle(input); if (errors.Count > 0) return Results.ValidationProblem(errors);
            article.Title = input.Title.Trim(); article.Body = input.Body.Trim(); article.Category = input.Category.Trim().ToLowerInvariant(); article.Visibility = input.Visibility; article.UpdatedAt = DateTime.UtcNow; article.Revision++;
            await db.SaveChangesAsync(); return Results.Ok(ArticleView(article));
        });

        api.MapDelete("/world-articles/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var article = await db.WorldArticles.SingleOrDefaultAsync(x => x.Id == id);
            if (article is null) return Results.NotFound();
            var access = await Access(db, article.WorldId, userId);
            if (!access.Member || !CanSee(article, access, userId)) return Results.NotFound();
            if (!CanEdit(article, access, userId)) return Results.Problem("Sem permissão para excluir artigo.", statusCode: 403);
            db.WorldArticles.Remove(article); await db.SaveChangesAsync(); return Results.NoContent();
        });

        api.MapGet("/world-articles/{id:guid}/links", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var article = await db.WorldArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (article is null) return Results.NotFound();
            var access = await Access(db, article.WorldId, userId);
            if (!access.Member || !CanSee(article, access, userId)) return Results.NotFound();
            var visibleIds = Visible(db.WorldArticles.AsNoTracking().Where(x => x.WorldId == article.WorldId), access, userId).Select(x => x.Id);
            var links = await db.WorldArticleLinks.AsNoTracking().Include(x => x.Source).Include(x => x.Target)
                .Where(x => (x.SourceId == id || x.TargetId == id) && visibleIds.Contains(x.SourceId) && visibleIds.Contains(x.TargetId))
                .OrderBy(x => x.CreatedAt).Take(200).ToListAsync();
            return Results.Ok(links.Select(x => LinkView(x, id)));
        });

        api.MapPost("/world-articles/{id:guid}/links", async Task<IResult> (Guid id, LinkInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var source = await db.WorldArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (source is null) return Results.NotFound();
            var access = await Access(db, source.WorldId, userId);
            if (!access.Member || !CanSee(source, access, userId)) return Results.NotFound();
            if (!CanEdit(source, access, userId)) return Results.Problem("Sem permissão para ligar este artigo.", statusCode: 403);
            if (input.TargetId == id || input.Label?.Trim().Length is not (>= 2 and <= 80))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["link"] = ["Selecione outro artigo e informe uma relação de 2 a 80 caracteres."] });
            var target = await db.WorldArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.TargetId && x.WorldId == source.WorldId);
            if (target is null || !CanSee(target, access, userId)) return Results.NotFound();
            var label = input.Label.Trim();
            if (await db.WorldArticleLinks.AnyAsync(x => x.SourceId == id && x.TargetId == input.TargetId && x.Label == label))
                return Results.Conflict(new { message = "Esta relação já existe." });
            var link = new WorldArticleLink { SourceId = id, TargetId = input.TargetId, Label = label };
            db.WorldArticleLinks.Add(link); await db.SaveChangesAsync();
            link.Source = source; link.Target = target;
            return Results.Created($"/api/world-articles/{id}/links", LinkView(link, id));
        });

        api.MapDelete("/world-article-links/{linkId:guid}", async Task<IResult> (Guid linkId, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var link = await db.WorldArticleLinks.Include(x => x.Source).Include(x => x.Target).SingleOrDefaultAsync(x => x.Id == linkId);
            if (link is null) return Results.NotFound();
            var access = await Access(db, link.Source.WorldId, userId);
            if (!access.Member || !CanSee(link.Source, access, userId) || !CanSee(link.Target, access, userId)) return Results.NotFound();
            if (!CanEdit(link.Source, access, userId)) return Results.Problem("Sem permissão para remover esta relação.", statusCode: 403);
            db.WorldArticleLinks.Remove(link); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static object View(World x) => new { x.Id, x.OwnerId, x.Name, x.Description, x.CreatedAt, x.UpdatedAt };
    private static object ArticleView(WorldArticle x) => new { x.Id, x.WorldId, x.AuthorId, x.Title, x.Body, x.Category, x.Visibility, x.Revision, x.CreatedAt, x.UpdatedAt };
    private static object LinkView(WorldArticleLink x, Guid currentId)
    {
        var other = x.SourceId == currentId ? x.Target : x.Source;
        return new { x.Id, x.Label, direction = x.SourceId == currentId ? "outgoing" : "incoming", article = new { other.Id, other.Title, other.Category, other.Visibility } };
    }
    private static async Task<WorldAccess> Access(AppDbContext db, Guid worldId, Guid userId)
    {
        var owner = await db.Worlds.AnyAsync(x => x.Id == worldId && x.OwnerId == userId);
        if (owner) return new(true, true, true, true);
        var roles = await db.CampaignMemberships.AsNoTracking().Where(x => x.UserId == userId && x.Campaign.WorldId == worldId).Select(x => x.Role).ToListAsync();
        return new(roles.Count > 0, roles.Any(x => x is "owner" or "gm" or "co-gm"), roles.Any(x => x != "spectator"), false);
    }
    private static bool CanSee(WorldArticle x, WorldAccess access, Guid userId) => x.Visibility == "campaign" || x.Visibility == "gm" && access.Master || x.Visibility == "private" && (x.AuthorId == userId || access.Master);
    private static bool CanEdit(WorldArticle x, WorldAccess access, Guid userId) => access.Master || access.Write && x.AuthorId == userId;
    private static IQueryable<WorldArticle> Visible(IQueryable<WorldArticle> query, WorldAccess access, Guid userId) => access.Master ? query : query.Where(x => x.Visibility == "campaign" || x.Visibility == "private" && x.AuthorId == userId);
    private static Dictionary<string, string[]> ValidateWorld(WorldInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 3 and <= 120) || input.Description is null || input.Description.Length > 4000) errors["world"] = ["Nome ou descrição inválidos."];
        return errors;
    }
    private static Dictionary<string, string[]> ValidateArticle(ArticleInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Title?.Trim().Length is not (>= 2 and <= 160)) errors["title"] = ["Título deve ter entre 2 e 160 caracteres."];
        if (input.Body is null || input.Body.Length > 40000) errors["body"] = ["Conteúdo excede 40.000 caracteres."];
        if (input.Category is null || !CategoryPattern.IsMatch(input.Category)) errors["category"] = ["Categoria inválida."];
        if (input.Visibility is not ("campaign" or "private" or "gm")) errors["visibility"] = ["Visibilidade inválida."];
        return errors;
    }
    private sealed record WorldAccess(bool Member, bool Master, bool Write, bool Owner);
    public sealed record WorldInput(string Name, string Description);
    public sealed record WorldLinkInput(Guid? WorldId);
    public sealed record ArticleInput(string Title, string Body, string Category, string Visibility, int Revision);
    public sealed record LinkInput(Guid TargetId, string Label);
}
