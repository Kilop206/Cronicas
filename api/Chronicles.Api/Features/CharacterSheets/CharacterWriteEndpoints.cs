using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterWrites(RouteGroupBuilder api)
    {
        api.MapPost("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId,
            CharacterInput input, HttpContext http, CreateCharacter command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var response = await command.Execute(campaignId, actor, input, http.RequestAborted);
            return CharacterWriteHttpResult.Legacy(response, actor, created: true);
        }).Produces<CharacterDocument>(201).Produces(401).Produces(403).Produces(404).ProducesValidationProblem();

        api.MapPut("/characters/{id:guid}", async Task<IResult> (Guid id, CharacterInput input, HttpContext http, SaveCharacter command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var response = await command.Execute(id, actor, input, ct: http.RequestAborted);
            return CharacterWriteHttpResult.Legacy(response, actor);
        }).Produces<CharacterDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
    }
}

internal static class CharacterWriteHttpResult
{
    internal static IResult? Error(CharacterWriteResponse response) => response.Failure switch
    {
        CharacterWriteFailure.NotFound => Results.NotFound(),
        CharacterWriteFailure.Forbidden => Results.Problem(response.Message, statusCode: 403),
        CharacterWriteFailure.Conflict => Results.Conflict(new { message = response.Message }),
        CharacterWriteFailure.Invalid => Results.ValidationProblem(response.Errors ?? new() { ["character"] = [response.Message ?? "Ficha inválida."] }),
        _ => null
    };

    internal static IResult Legacy(CharacterWriteResponse response, Guid actor, bool created = false)
    {
        if (Error(response) is { } error) return error;
        var document = CharacterSheetProjection.Document(response.Character!, actor);
        return created ? Results.Created($"/api/characters/{document.Id}", document) : Results.Ok(document);
    }
}
