using Chronicles.Application.Characters;
using Chronicles.Domain;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterCompanions(RouteGroupBuilder api)
    {
        api.MapGet("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, ManageCharacterFavorites query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await query.Read(id, actor, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CharacterFavoriteDocument>().Produces(401).Produces(404);
        api.MapPut("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, ManageCharacterFavorites command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Set(id, actor, true, http.RequestAborted), _ => Results.NoContent());
        }).Produces(204).Produces(401).Produces(404).Produces(409);
        api.MapDelete("/characters/{id:guid}/favorite", async Task<IResult> (Guid id, HttpContext http, ManageCharacterFavorites command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CharacterAssetHttpResult.Complete(await command.Set(id, actor, false, http.RequestAborted), _ => Results.NoContent());
        }).Produces(204).Produces(401).Produces(404).Produces(409);
        api.MapGet("/characters/{id:guid}/secret", async Task<IResult> (Guid id, HttpContext http, ManageCharacterSecrets query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var result = await query.Read(id, actor, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CharacterSecretDocument>().Produces(401).Produces(404);
        api.MapPut("/characters/{id:guid}/secret", async Task<IResult> (Guid id, SecretInput input, HttpContext http, ManageCharacterSecrets command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            return CharacterAssetHttpResult.Complete(await command.Save(id, actor, new CharacterSecretValues(input.Notes, input.Revision, input.ExpectedExists), http.RequestAborted), Results.Ok);
        }).Produces<CharacterSecretDocument>().Produces(401).Produces(404).Produces(409).ProducesValidationProblem();
        api.MapPost("/characters/{id:guid}/overlay-key", async Task<IResult> (Guid id, HttpContext http, ManageCharacterOverlay command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            return CharacterAssetHttpResult.Complete(await command.Rotate(id, actor, http.RequestAborted), Results.Ok);
        }).Produces<CharacterOverlayKey>().Produces(401).Produces(404).Produces(409);
        api.MapGet("/overlay/{key}", async Task<IResult> (string key, HttpContext http, ManageCharacterOverlay query) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await query.Read(key, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CharacterOverlayDocument>().Produces(404);
    }

    public sealed record SecretInput(string Notes, int Revision, bool? ExpectedExists = null);
}
