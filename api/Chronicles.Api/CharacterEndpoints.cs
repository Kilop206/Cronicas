using System.Security.Claims;
using System.Security.Cryptography;
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
            var items = characters.Select(View).ToList();
            return Results.Ok(items);
        });

        api.MapPost("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId, CharacterInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await Member(db, campaignId, userId)) return Results.NotFound();
            var error = Validate(input);
            if (error is not null) return Results.ValidationProblem(error);
            var character = new Character { CampaignId = campaignId, OwnerId = userId, Name = input.Name.Trim() };
            Apply(character, input);
            db.Characters.Add(character);
            await db.SaveChangesAsync();
            return Results.Created($"/api/characters/{character.Id}", View(character));
        });

        api.MapGet("/characters/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.AsNoTracking().Where(x => x.Id == id && x.Campaign.Memberships.Any(m => m.UserId == userId)).SingleOrDefaultAsync();
            return character is null ? Results.NotFound() : Results.Ok(View(character));
        });

        api.MapPut("/characters/{id:guid}", async Task<IResult> (Guid id, CharacterInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var character = await db.Characters.Include(x => x.Campaign).ThenInclude(x => x.Memberships).SingleOrDefaultAsync(x => x.Id == id);
            if (character is null || !character.Campaign.Memberships.Any(x => x.UserId == userId)) return Results.NotFound();
            if (character.OwnerId != userId && !character.Campaign.Memberships.Any(x => x.UserId == userId && x.Role is "owner" or "gm" or "co-gm")) return Results.Forbid();
            var error = Validate(input);
            if (error is not null) return Results.ValidationProblem(error);
            Apply(character, input);
            await db.SaveChangesAsync();
            return Results.Ok(View(character));
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
    private static object View(Character x) => new { x.Id, x.CampaignId, x.OwnerId, x.Name, x.PortraitUrl, x.BackgroundUrl, x.Health, x.MaxHealth, x.Sanity, x.MaxSanity, x.Mana, x.MaxMana, x.UpdatedAt };
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
    private static void Apply(Character target, CharacterInput input)
    {
        target.Name = input.Name.Trim();
        target.PortraitUrl = input.PortraitUrl?.Trim();
        target.BackgroundUrl = input.BackgroundUrl?.Trim();
        target.Health = input.Health; target.MaxHealth = input.MaxHealth;
        target.Sanity = input.Sanity; target.MaxSanity = input.MaxSanity;
        target.Mana = input.Mana; target.MaxMana = input.MaxMana;
        target.UpdatedAt = DateTime.UtcNow;
    }
    public sealed record CharacterInput(string Name, string? PortraitUrl, string? BackgroundUrl, int Health, int MaxHealth, int Sanity, int MaxSanity, int Mana, int MaxMana);
}
