using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterReads(RouteGroupBuilder api)
    {
        api.MapGet("/characters/{id:guid}", async Task<IResult> (Guid id, HttpContext http, ReadCharacter query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var character = await query.Document(id, actor, http.RequestAborted);
            return character is null ? Results.NotFound() : Results.Ok(character);
        }).Produces<CharacterDocument>().Produces(401).Produces(404);
    }
}
