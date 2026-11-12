using Chronicles.Application.Characters;
using Chronicles.Domain;

namespace Chronicles.Api;

public static class CharacterInventoryEndpoints
{
    public static void MapCharacterInventoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/characters/{characterId:guid}/inventory", async Task<IResult> (Guid characterId, HttpContext http, ManageCharacterInventory query) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            var items = await query.List(characterId, actor, http.RequestAborted);
            return items is null ? Results.NotFound() : Results.Ok(items);
        }).Produces<InventoryItemDocument[]>().Produces(401).Produces(404);

        api.MapPost("/characters/{characterId:guid}/inventory", async Task<IResult> (Guid characterId, InventoryInput input, HttpContext http, ManageCharacterInventory command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Create(characterId, actor, input.Values(), http.RequestAborted),
                item => Results.Created($"/api/characters/{characterId}/inventory/{item.Id}", item));
        }).Produces<InventoryItemDocument>(201).Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapPut("/characters/{characterId:guid}/inventory/{itemId:guid}", async Task<IResult> (Guid characterId, Guid itemId,
            InventoryInput input, HttpContext http, ManageCharacterInventory command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Update(characterId, itemId, actor, input.Version, input.Values(), http.RequestAborted),
                item => Results.Ok(item));
        }).Produces<InventoryItemDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapDelete("/characters/{characterId:guid}/inventory/{itemId:guid}", async Task<IResult> (Guid characterId, Guid itemId,
            int version, HttpContext http, ManageCharacterInventory command) =>
        {
            if (CharacterAssetHttpResult.Actor(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Delete(characterId, itemId, actor, version, http.RequestAborted),
                _ => Results.NoContent());
        }).Produces(204).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    public sealed record InventoryInput(string Name, string Category, int Quantity, decimal Weight, string Notes, int Version)
    {
        public InventoryItemValues Values() => new(Name, Category, Quantity, Weight, Notes);
    }
}
