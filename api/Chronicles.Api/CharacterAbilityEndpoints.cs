using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class CharacterAbilityEndpoints
{
    public static void MapCharacterAbilityEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/characters/{characterId:guid}/abilities", async Task<IResult> (Guid characterId, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await CharacterResourceAccess.CanRead(db, characterId, userId)) return Results.NotFound();
            var items = await db.CharacterAbilities.AsNoTracking().Where(x => x.CharacterId == characterId).OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(items.Select(View));
        });

        api.MapPost("/characters/{characterId:guid}/abilities", async Task<IResult> (Guid characterId, AbilityInput input, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar as habilidades deste personagem.", statusCode: 403);
            var errors = Validate(input);
            if (errors is not null) return Results.ValidationProblem(errors);
            if (await db.CharacterAbilities.CountAsync(x => x.CharacterId == characterId) >= 100)
                return Results.Conflict(new { message = "A ficha atingiu o limite de 100 habilidades." });
            var ability = new CharacterAbility { CharacterId = characterId, Name = input.Name.Trim() };
            Apply(ability, input);
            db.CharacterAbilities.Add(ability);
            await db.SaveChangesAsync();
            return Results.Created($"/api/characters/{characterId}/abilities/{ability.Id}", View(ability));
        });

        api.MapPut("/characters/{characterId:guid}/abilities/{abilityId:guid}", async Task<IResult> (Guid characterId, Guid abilityId, AbilityInput input, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar as habilidades deste personagem.", statusCode: 403);
            var ability = await db.CharacterAbilities.SingleOrDefaultAsync(x => x.Id == abilityId && x.CharacterId == characterId);
            if (ability is null) return Results.NotFound();
            if (ability.Version != input.Version) return Results.Conflict(new { message = "A habilidade mudou. Atualize antes de salvar." });
            var errors = Validate(input);
            if (errors is not null) return Results.ValidationProblem(errors);
            Apply(ability, input);
            ability.Version++; ability.UpdatedAt = DateTime.UtcNow;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A habilidade mudou. Atualize antes de salvar." }); }
            return Results.Ok(View(ability));
        });

        api.MapDelete("/characters/{characterId:guid}/abilities/{abilityId:guid}", async Task<IResult> (Guid characterId, Guid abilityId, int version, HttpContext http, AppDbContext db) =>
        {
            if (CharacterResourceAccess.UserId(http) is not { } userId) return Results.Unauthorized();
            var access = await CharacterResourceAccess.CanEdit(db, characterId, userId);
            if (access is null) return Results.NotFound();
            if (!access.Value) return Results.Problem("Você não pode editar as habilidades deste personagem.", statusCode: 403);
            var ability = await db.CharacterAbilities.SingleOrDefaultAsync(x => x.Id == abilityId && x.CharacterId == characterId);
            if (ability is null) return Results.NotFound();
            if (ability.Version != version) return Results.Conflict(new { message = "A habilidade mudou. Atualize antes de excluir." });
            db.CharacterAbilities.Remove(ability);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "A habilidade mudou. Atualize antes de excluir." }); }
            return Results.NoContent();
        });
    }

    private static Dictionary<string, string[]>? Validate(AbilityInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 1 and <= 120)) errors["name"] = ["Informe um nome com até 120 caracteres."];
        if (input.Description is null || input.Description.Length > 4000 || input.Effect is null || input.Effect.Length > 4000 ||
            input.Cost is null || input.Cost.Length > 80 || input.Duration is null || input.Duration.Length > 80 ||
            input.Limitations is null || input.Limitations.Length > 1000 || input.Recharge is null || input.Recharge.Length > 80)
            errors["ability"] = ["Um ou mais campos excedem o limite."];
        if (!string.IsNullOrWhiteSpace(input.RollExpression) &&
            (input.RollExpression.Length > 40 || !DiceExpression.TryParse(input.RollExpression, out _)))
            errors["rollExpression"] = ["Use uma expressão como 1d20+5 ou 2d6."];
        return errors.Count == 0 ? null : errors;
    }

    private static void Apply(CharacterAbility target, AbilityInput input)
    {
        target.Name = input.Name.Trim(); target.Description = input.Description.Trim(); target.Cost = input.Cost.Trim();
        target.Effect = input.Effect.Trim(); target.Duration = input.Duration.Trim(); target.Limitations = input.Limitations.Trim();
        target.Recharge = input.Recharge.Trim(); target.RollExpression = string.IsNullOrWhiteSpace(input.RollExpression) ? null : input.RollExpression.Trim().ToLowerInvariant();
    }

    private static object View(CharacterAbility x) => new { x.Id, x.CharacterId, x.Name, x.Description, x.Cost, x.Effect, x.Duration,
        x.Limitations, x.Recharge, x.RollExpression, x.Version, x.CreatedAt, x.UpdatedAt };

    public sealed record AbilityInput(string Name, string Description, string Cost, string Effect, string Duration,
        string Limitations, string Recharge, string? RollExpression, int Version);
}
