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
        api.MapGet("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await Member(db, campaignId, userId)) return Results.NotFound();
            var characters = await db.Characters.AsNoTracking().Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.Name).ToListAsync();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId).Select(x => x.Role).SingleAsync();
            var items = characters.Select(x => View(x, role != "spectator" && (x.OwnerId == userId || role is "owner" or "gm" or "co-gm"))).ToList();
            return Results.Ok(items);
        });

        api.MapGet("/characters/{id:guid}/revisions", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (character is null) return Results.NotFound();
            var role = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == character.CampaignId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync();
            if (role is null or "spectator" || character.OwnerId != userId && role is not ("owner" or "gm" or "co-gm")) return Results.NotFound();
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
            if (role is null or "spectator" || character.OwnerId != userId && role is not ("owner" or "gm" or "co-gm")) return Results.NotFound();
            if (input.ExpectedVersion != character.Version) return Results.Conflict(new { message = "A ficha mudou. Atualize antes de restaurar." });
            var revision = await db.CharacterRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == revisionId && x.CharacterId == id);
            if (revision is null) return Results.NotFound();
            var snapshot = JsonSerializer.Deserialize<CharacterSnapshot>(revision.SnapshotJson);
            if (snapshot is null) return Results.Problem("Revisão inválida.", statusCode: 500);
            var restoredValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(snapshot.FieldsJson) ?? [];
            var fieldErrors = await ValidateFields(db, character.CampaignId, restoredValues, snapshot.FieldsJson,
                allowGm: role is "owner" or "gm" or "co-gm", authorizationJson: character.FieldsJson);
            if (fieldErrors is not null) return Results.ValidationProblem(fieldErrors);
            CharacterHistory.Capture(db, character, userId);
            CharacterHistory.Restore(character, snapshot with { FieldsJson = JsonSerializer.Serialize(restoredValues) });
            try{if(await RulesDefinition(db,character.CampaignId) is {} ruleDefinition)await CharacterEncounterConditions.Sync(db,character,ruleDefinition);}
            catch(ArgumentException exception){return RuleError(exception.Message);}
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
            if (role is null or "spectator") return Results.NotFound();
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
            var abilities = await db.CharacterAbilities.AsNoTracking().Where(x => x.CharacterId == id).ToListAsync();
            db.Characters.Add(copy);
            foreach (var item in inventory)
                db.CharacterInventoryItems.Add(new CharacterInventoryItem
                {
                    CharacterId = copy.Id, Name = item.Name, Category = item.Category,
                    Quantity = item.Quantity, Weight = item.Weight, Notes = item.Notes
                });
            foreach (var ability in abilities)
                db.CharacterAbilities.Add(new CharacterAbility
                {
                    CharacterId = copy.Id, Name = ability.Name, Description = ability.Description, Cost = ability.Cost,
                    Effect = ability.Effect, Duration = ability.Duration, Limitations = ability.Limitations,
                    Recharge = ability.Recharge, RollExpression = ability.RollExpression
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
    private static Task<SystemDefinition?> RulesDefinition(AppDbContext db, Guid campaignId) => CharacterEncounterConditions.Definition(db, campaignId);
    private static CharacterInput RuleInput(Character c) => new(c.Name, c.PortraitUrl, c.BackgroundUrl, c.Health, c.MaxHealth,
        c.Sanity, c.MaxSanity, c.Mana, c.MaxMana, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.FieldsJson), c.Version);
    private static Task<bool> Member(AppDbContext db, Guid campaignId, Guid userId) => db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId);
    private static object View(Character x, bool canEdit = false) => new { x.Id, x.CampaignId, x.OwnerId, x.Name, x.PortraitUrl, x.BackgroundUrl, x.Health, x.MaxHealth, x.Sanity, x.MaxSanity, x.Mana, x.MaxMana, fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(x.FieldsJson), x.Version, x.UpdatedAt, canEdit };
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
    private static async Task<Dictionary<string, string[]>?> ValidateFields(AppDbContext db, Guid campaignId, Dictionary<string, JsonElement>? values, string? previousJson = null,
        bool allowGm = false, string? authorizationJson = null, bool ruleExecution = false)
    {
        var definition = await CharacterEncounterConditions.Definition(db, campaignId);
        return CharacterSheetValidation.ValidateFields(definition, values,
            previousJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(previousJson), allowGm,
            authorizationJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(authorizationJson), ruleExecution,
            newCharacter: previousJson is null);
    }
    public sealed record RestoreInput(int ExpectedVersion);
    public sealed record SecretInput(string Notes, int Revision);
}
