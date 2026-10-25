using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await Member(db, campaignId, userId)) return Results.NotFound();
            var characters = await db.Characters.AsNoTracking().Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.Name).ToListAsync();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleAsync();
            var items = characters.Select(x => View(x, x.OwnerId == userId || role is "owner" or "gm" or "co-gm")).ToList();
            return Results.Ok(items);
        });

        api.MapPost("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId, CharacterInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null) return Results.NotFound();
            if (role is "spectator") return Results.Problem("Espectadores não podem criar personagens.", statusCode: 403);
            var error = Validate(input);
            if (error is not null) return Results.ValidationProblem(error);
            var fieldErrors = await ValidateFields(db, campaignId, input.Fields);
            if (fieldErrors is not null) return Results.ValidationProblem(fieldErrors);
            var character = new Character { CampaignId = campaignId, OwnerId = userId, Name = input.Name.Trim() };
            Apply(character, input);
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            return Results.Created($"/api/characters/{character.Id}", View(character));
        });

        api.MapGet("/characters/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (character is null) return Results.NotFound();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == character.CampaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null) return Results.NotFound();
            return Results.Ok(View(character, character.OwnerId == userId || role is "owner" or "gm" or "co-gm"));
        });

        api.MapPut("/characters/{id:guid}", async Task<IResult> (Guid id, CharacterInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.Include(x => x.Campaign).ThenInclude(x => x.Memberships).SingleOrDefaultAsync(x => x.Id == id);
            if (character is null || !character.Campaign.Memberships.Any(x => x.UserId == userId)) return Results.NotFound();
            if (character.OwnerId != userId && !character.Campaign.Memberships.Any(x => x.UserId == userId && x.Role is "owner" or "gm" or "co-gm")) return Results.Problem("Você não pode editar este personagem.", statusCode: 403);
            if (input.Version is { } expected && expected != character.Version) return Results.Conflict(new { message = "A ficha mudou. Atualize antes de salvar." });
            var error = Validate(input);
            if (error is not null) return Results.ValidationProblem(error);
            var fieldErrors = await ValidateFields(db, character.CampaignId, input.Fields, character.FieldsJson);
            if (fieldErrors is not null) return Results.ValidationProblem(fieldErrors);
            CharacterHistory.Capture(db, character, userId);
            Apply(character, input);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A ficha mudou. Atualize antes de salvar." }); }
            return Results.Ok(View(character, true));
        });

        api.MapGet("/characters/{id:guid}/revisions", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (character is null) return Results.NotFound();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == character.CampaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null || character.OwnerId != userId && role is not ("owner" or "gm" or "co-gm")) return Results.NotFound();
            var revisions = await db.CharacterRevisions.AsNoTracking().Where(x => x.CharacterId == id).OrderByDescending(x => x.Version).Take(100)
                .Select(x => new { x.Id, x.Version, x.CreatedAt, editedBy = x.EditedBy.DisplayName }).ToListAsync();
            return Results.Ok(revisions);
        });

        api.MapPost("/characters/{id:guid}/revisions/{revisionId:guid}/restore", async Task<IResult> (Guid id, Guid revisionId, RestoreInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.SingleOrDefaultAsync(x => x.Id == id);
            if (character is null) return Results.NotFound();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == character.CampaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null || character.OwnerId != userId && role is not ("owner" or "gm" or "co-gm")) return Results.NotFound();
            if (input.ExpectedVersion != character.Version) return Results.Conflict(new { message = "A ficha mudou. Atualize antes de restaurar." });
            var revision = await db.CharacterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == revisionId && x.CharacterId == id);
            if (revision is null) return Results.NotFound();
            var snapshot = JsonSerializer.Deserialize<CharacterSnapshot>(revision.SnapshotJson);
            if (snapshot is null) return Results.Problem("Revisão inválida.", statusCode: 500);
            CharacterHistory.Capture(db, character, userId);
            CharacterHistory.Restore(character, snapshot);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A ficha mudou. Atualize antes de restaurar." }); }
            return Results.Ok(View(character, true));
        });

        api.MapPost("/characters/{id:guid}/duplicate", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var source = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (source is null) return Results.NotFound();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == source.CampaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null) return Results.NotFound();
            if (source.OwnerId != userId && role is not ("owner" or "gm" or "co-gm"))
                return Results.Problem("Você não pode duplicar este personagem.", statusCode: 403);
            var copy = new Character
            {
                CampaignId = source.CampaignId, OwnerId = userId,
                Name = $"{source.Name[..Math.Min(source.Name.Length, 110)]} (cópia)",
                PortraitUrl = source.PortraitUrl, BackgroundUrl = source.BackgroundUrl,
                Health = source.Health, MaxHealth = source.MaxHealth,
                Sanity = source.Sanity, MaxSanity = source.MaxSanity,
                Mana = source.Mana, MaxMana = source.MaxMana,
                FieldsJson = source.FieldsJson
            };
            var inventory = await db.CharacterInventoryItems.AsNoTracking().Where(x => x.CharacterId == id).ToListAsync();
            db.Characters.Add(copy);
            foreach (var item in inventory)
                db.CharacterInventoryItems.Add(new CharacterInventoryItem
                {
                    CharacterId = copy.Id, Name = item.Name, Category = item.Category,
                    Quantity = item.Quantity, Weight = item.Weight, Notes = item.Notes
                });
            await db.SaveChangesAsync();
            return Results.Created($"/api/characters/{copy.Id}", View(copy, true));
        });

        api.MapGet("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.Characters.AnyAsync(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId))) return Results.NotFound();
            return Results.Ok(new { favorite = await db.CharacterFavorites.AnyAsync(x => x.UserId == userId && x.CharacterId == id) });
        });

        api.MapPut("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.Characters.AnyAsync(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId))) return Results.NotFound();
            if (!await db.CharacterFavorites.AnyAsync(x => x.UserId == userId && x.CharacterId == id))
            {
                db.CharacterFavorites.Add(new CharacterFavorite { UserId = userId, CharacterId = id });
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        api.MapDelete("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.Characters.AnyAsync(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId))) return Results.NotFound();
            await db.CharacterFavorites.Where(x => x.UserId == userId && x.CharacterId == id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

        api.MapGet("/characters/{id:guid}/secret", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var allowed = await db.Characters.AsNoTracking().AnyAsync(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId &&
                (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm")));
            if (!allowed) return Results.NotFound();
            var secret = await db.CharacterSecrets.AsNoTracking().SingleOrDefaultAsync(x => x.CharacterId == id);
            return Results.Ok(secret is null ? new { notes = "", revision = 1, updatedAt = (DateTime?)null } :
                new { notes = secret.Notes, revision = secret.Revision, updatedAt = (DateTime?)secret.UpdatedAt });
        });

        api.MapPut("/characters/{id:guid}/secret", async Task<IResult> (Guid id, SecretInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var allowed = await db.Characters.AsNoTracking().AnyAsync(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId &&
                (m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm")));
            if (!allowed) return Results.NotFound();
            if (input.Notes is null || input.Notes.Length > 10000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["notes"] = ["Anotações excedem 10.000 caracteres."] });
            var secret = await db.CharacterSecrets.SingleOrDefaultAsync(x => x.CharacterId == id);
            if (secret is null)
            {
                if (input.Revision != 1) return Results.Conflict(new { message = "As anotações mudaram. Atualize antes de salvar." });
                secret = new CharacterSecret { CharacterId = id, Notes = input.Notes.Trim() };
                db.CharacterSecrets.Add(secret);
            }
            else
            {
                if (input.Revision != secret.Revision) return Results.Conflict(new { message = "As anotações mudaram. Atualize antes de salvar." });
                secret.Notes = input.Notes.Trim(); secret.Revision++; secret.UpdatedAt = DateTime.UtcNow;
            }
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "As anotações mudaram. Atualize antes de salvar." }); }
            catch (DbUpdateException) { return Results.Conflict(new { message = "As anotações mudaram. Atualize antes de salvar." }); }
            return Results.Ok(new { secret.Notes, secret.Revision, secret.UpdatedAt });
        });

        api.MapPost("/characters/{id:guid}/overlay-key", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && x.Campaign.Memberships.Any(m => m.UserId == userId));
            if (character is null) return Results.NotFound();
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            character.OverlayKeyHash = Hash(key);
            await db.SaveChangesAsync();
            return Results.Ok(new { key });
        });

        // The capability grants read access only to the safe overlay projection, never to a campaign or sheet.
        api.MapGet("/overlay/{key}", async Task<IResult> (string key, AppDbContext db) =>
        {
            if (key.Length != 64 || !key.All(Uri.IsHexDigit)) return Results.NotFound();
            var hash = Hash(key.ToLowerInvariant());
            var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.OverlayKeyHash == hash);
            return character is null ? Results.NotFound() : Results.Ok(new
            {
                character.Name, character.PortraitUrl, character.BackgroundUrl,
                character.Health, character.MaxHealth, character.Sanity, character.MaxSanity,
                character.Mana, character.MaxMana, character.UpdatedAt
            });
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static Task<bool> Member(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId);
    private static object View(Character x, bool canEdit = false) => new { x.Id, x.CampaignId, x.OwnerId, x.Name, x.PortraitUrl, x.BackgroundUrl, x.Health, x.MaxHealth, x.Sanity, x.MaxSanity, x.Mana, x.MaxMana, fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x.FieldsJson), x.Version, x.UpdatedAt, canEdit };
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    private static Dictionary<string, string[]>? Validate(CharacterInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 2 and <= 120)) errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
        if (!ValidUrl(input.PortraitUrl) || !ValidUrl(input.BackgroundUrl)) errors["image"] = ["Use URLs HTTPS de imagens com até 2048 caracteres."];
        if (!ValidResource(input.Health, input.MaxHealth) || !ValidResource(input.Sanity, input.MaxSanity) || !ValidResource(input.Mana, input.MaxMana)) errors["resources"] = ["Cada recurso deve estar entre zero e seu máximo (até 9999)."];
        return errors.Count == 0 ? null : errors;
    }
    private static bool ValidUrl(string? value) => string.IsNullOrWhiteSpace(value) || value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
    private static bool ValidResource(int value, int max) => max is >= 1 and <= 9999 && value >= 0 && value <= max;
    private static async Task<Dictionary<string, string[]>?> ValidateFields(AppDbContext db, Guid campaignId, Dictionary<string, JsonElement>? values, string? previousJson = null)
    {
        if (values is null) return null;
        if (values.Count > 50 || JsonSerializer.Serialize(values).Length > 20000) return new() { ["fields"] = ["Campos excedem o limite."] };
        var systemId = await db.Campaigns.Where(x => x.Id == campaignId).Select(x => x.SystemId).SingleAsync() ?? SystemCatalog.GenericId;
        var schema = await db.RpgSystems.Where(x => x.Id == systemId).Select(x => x.SchemaJson).SingleAsync();
        var fields = JsonSerializer.Deserialize<SystemField[]>(schema) ?? [];
        var byKey = fields.ToDictionary(x => x.Key);
        var previous = previousJson is null ? new Dictionary<string, JsonElement>() : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(previousJson) ?? [];
        foreach (var (key, value) in values)
        {
            if (!byKey.TryGetValue(key, out var field))
            {
                if (previous.TryGetValue(key, out var oldValue) && JsonElement.DeepEquals(value, oldValue)) continue;
                return new() { ["fields"] = [$"Campo desconhecido: {key}."] };
            }
            var valid = field.Type switch
            {
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && (field.Min is null || number >= field.Min) && (field.Max is null || number <= field.Max),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "select" => value.ValueKind == JsonValueKind.String && field.Options?.Contains(value.GetString()) == true,
                "longtext" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 10000,
                "text" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 300,
                _ => false
            };
            if (!valid) return new() { ["fields"] = [$"Valor inválido para {field.Label}."] };
        }
        return null;
    }
    private static void Apply(Character target, CharacterInput input)
    {
        target.Name = input.Name.Trim();
        target.PortraitUrl = input.PortraitUrl?.Trim();
        target.BackgroundUrl = input.BackgroundUrl?.Trim();
        target.Health = input.Health; target.MaxHealth = input.MaxHealth;
        target.Sanity = input.Sanity; target.MaxSanity = input.MaxSanity;
        target.Mana = input.Mana; target.MaxMana = input.MaxMana;
        if (input.Fields is not null) target.FieldsJson = JsonSerializer.Serialize(input.Fields);
        target.UpdatedAt = DateTime.UtcNow;
    }
    public sealed record CharacterInput(string Name, string? PortraitUrl, string? BackgroundUrl, int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana, Dictionary<string, JsonElement>? Fields = null, int? Version = null);
    public sealed record RestoreInput(int ExpectedVersion);
    public sealed record SecretInput(string Notes, int Revision);
}
