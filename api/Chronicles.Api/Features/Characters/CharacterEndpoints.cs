using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this RouteGroupBuilder api)
    {
        MapRuleActions(api);
        MapCharacterWrites(api);
        MapCharacterReads(api);
        MapCharacterLifecycle(api);
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
            var character = await db.Characters.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && x.Campaign.Memberships.Any(m => m.UserId == userId && m.Role != "spectator"));
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
            if (character is null) return Results.NotFound();
            var systemId = await db.Campaigns.AsNoTracking().Where(x => x.Id == character.CampaignId).Select(x => x.SystemId).SingleAsync();
            var schema = systemId is null ? null : await db.RpgSystems.AsNoTracking().Where(x => x.Id == systemId).Select(x => x.SchemaJson).SingleOrDefaultAsync();
            var definition = schema is null ? null : SystemDefinition.Read(schema);
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.FieldsJson) ?? [];
            var customRenderer = !SpecializedRenderer(definition);
            var resourceKeys = (customRenderer ? null : definition?.TemplateId) switch
            {
                "ordem_paranormal" => new[] { (Key: "health", Label: "PV"), (Key: "mana", Label: "PE"), (Key: "sanity", Label: "SAN") },
                "dnd_5e" => [("health", "HP")],
                "tormenta20" => [("health", "PV"), ("mana", "PM")],
                "call_of_cthulhu_7e" => [("health", "HP"), ("sanity", "SAN"), ("mana", "Magic Points")],
                _ => definition?.Layout is { } layout
                    ? layout.Pages.SelectMany(page => page.Blocks).Where(block => block.Kind == "resource" && block.Key is "health" or "sanity" or "mana")
                        .GroupBy(block => block.Key).Select(group => (Key: group.Key!, Label: group.First().Title ?? "Recurso")).ToArray()
                    : [("health", "Vida"), ("sanity", "Sanidade"), ("mana", "Mana")]
            };
            var indicatorKeys = (customRenderer ? null : definition?.TemplateId) switch
            {
                "ordem_paranormal" => new[] { (Key: "nex", Label: "NEX"), (Key: "defesa", Label: "Defesa") },
                "dnd_5e" => [("nivel", "Nível"), ("classe_armadura", "AC"), ("iniciativa", "Iniciativa")],
                "tormenta20" => [("nivel", "Nível"), ("defesa", "Defesa")],
                "call_of_cthulhu_7e" => [("sorte", "Sorte")],
                _ => definition?.Layout is { } indicatorLayout
                    ? indicatorLayout.Pages.SelectMany(page => page.Blocks)
                        .Where(block => block.Kind == "field" && block.Style == "score" && block.Key is not null)
                        .GroupBy(block => block.Key).Select(group => (Key: group.Key!, Label: group.First().Title ?? definition.Fields.FirstOrDefault(field => field.Key == group.Key)?.Label ?? "Indicador")).Take(6).ToArray()
                    : []
            };
            var resources = customRenderer && definition?.Layout is { } customLayout
                ? customLayout.Pages.SelectMany(page => page.Blocks)
                    .Where(block => block.Kind is "resource" or "tracker" && block.Key is not null)
                    .GroupBy(block => block.Key!).Select(group => group.First())
                    .Select(block => new { Label = block.Title ?? definition.Fields.FirstOrDefault(field => field.Key == block.Key)?.Label ?? "Recurso",
                        current = ResourceValue(character, values, block.Key!), max = block.MaxKey is { } maxKey ? ResourceValue(character, values, maxKey) : ResourceMax(character, block.Key!) })
                : resourceKeys.Select(item => new { item.Label, current = ResourceValue(character, values, item.Key), max = ResourceMax(character, item.Key) });
            var conditionState = definition is not null && SheetRuleActions.Supports(definition) && definition.TemplateId is "ordem_paranormal" or "tormenta20" ? new RuleState(RuleInput(character)) : null;
            var indicators = indicatorKeys.Where(item => values.ContainsKey(item.Key)).Select(item => new { item.Label, value = item.Key == "defesa" && conditionState is not null ? D20Conditions.Defense(conditionState, false, definition!.TemplateId == "ordem_paranormal").ToString() : values[item.Key].ToString() });
            return Results.Ok(new
            {
                character.Name, character.PortraitUrl, character.BackgroundUrl,
                character.Health, character.MaxHealth, character.Sanity, character.MaxSanity,
                character.Mana, character.MaxMana, character.UpdatedAt, templateId = definition?.TemplateId,
                resources, indicators
            });
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static CharacterInput RuleInput(Character c) => new(c.Name, c.PortraitUrl, c.BackgroundUrl, c.Health, c.MaxHealth,
        c.Sanity, c.MaxSanity, c.Mana, c.MaxMana, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson), c.Version);
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
    private static int ResourceValue(Character character, Dictionary<string, JsonElement> values, string key) => key switch { "health" => character.Health, "sanity" => character.Sanity, "mana" => character.Mana, _ => values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0 };
    private static int ResourceMax(Character character, string key) => key switch { "health" => character.MaxHealth, "sanity" => character.MaxSanity, "mana" => character.MaxMana, _ => 1 };
    private static bool SpecializedRenderer(SystemDefinition? definition)
    {
        if (definition is null || definition.RendererMode == "layout") return false;
        return definition.TemplateId switch
        {
            "ordem_paranormal" => definition.TemplateVersion is >= 1 and <= 7,
            "dnd_5e" => definition.TemplateVersion is >= 1 and <= 5,
            "tormenta20" => definition.TemplateVersion is >= 1 and <= 4,
            "call_of_cthulhu_7e" => definition.TemplateVersion is >= 1 and <= 6,
            _ => false
        };
    }
    public sealed record SecretInput(string Notes, int Revision);
}
