using Chronicles.Application.Characters;
using Chronicles.Domain;

namespace Chronicles.Api;

public static class CharacterAbilityEndpoints
{
    public static void MapCharacterAbilityEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/characters/{characterId:guid}/abilities", async Task<IResult> (Guid characterId, HttpContext http, ManageCharacterAbilities query) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            var items = await query.List(characterId, actor, http.RequestAborted);
            return items is null ? Results.NotFound() : Results.Ok(items);
        }).Produces<CharacterAbilityDocument[]>().Produces(401).Produces(404);

        api.MapPost("/characters/{characterId:guid}/abilities", async Task<IResult> (Guid characterId, AbilityInput input, HttpContext http, ManageCharacterAbilities command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Create(characterId, actor, input.Values(), http.RequestAborted),
                item => Results.Created($"/api/characters/{characterId}/abilities/{item.Id}", item));
        }).Produces<CharacterAbilityDocument>(201).Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapPut("/characters/{characterId:guid}/abilities/{abilityId:guid}", async Task<IResult> (Guid characterId, Guid abilityId,
            AbilityInput input, HttpContext http, ManageCharacterAbilities command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Update(characterId, abilityId, actor, input.Version, input.Values(), http.RequestAborted),
                item => Results.Ok(item));
        }).Produces<CharacterAbilityDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapDelete("/characters/{characterId:guid}/abilities/{abilityId:guid}", async Task<IResult> (Guid characterId, Guid abilityId,
            int version, HttpContext http, ManageCharacterAbilities command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Delete(characterId, abilityId, actor, version, http.RequestAborted),
                _ => Results.NoContent());
        }).Produces(204).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    public sealed record AbilityInput(string Name, string Description, string Cost, string Effect, string Duration,
        string Limitations, string Recharge, string? RollExpression, int Version)
    {
        public CharacterAbilityValues Values() => new(Name, Description, Cost, Effect, Duration, Limitations, Recharge, RollExpression);
    }
}
