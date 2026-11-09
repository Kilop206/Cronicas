using System.Text.Json.Serialization;
using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterSystems(RouteGroupBuilder api)
    {
        api.MapGet("/characters/{id:guid}/system", async Task<IResult> (Guid id, HttpContext http, ReadCharacterSystem query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await query.Read(id, actor, http.RequestAborted);
            http.Response.Headers.CacheControl = "private, no-store";
            return !result.Accessible ? Results.NotFound() : Results.Ok(result.System);
        }).Produces<CharacterSystemDocument>().Produces(401).Produces(404);
        api.MapPost("/v1/characters/{id:guid}/system-version", async Task<IResult> (Guid id, PinSystemInput input,
            HttpContext http, PinCharacterSystem command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            return CharacterWriteHttpResult.Legacy(await command.Execute(id, actor, input.ExpectedVersion, input.RegistrySystemId, input.TemplateRevision, http.RequestAborted), actor);
        }).Produces<CharacterDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
    }
    public sealed record PinSystemInput([property: JsonRequired] int ExpectedVersion, [property: JsonRequired] Guid RegistrySystemId,
        [property: JsonRequired] int TemplateRevision);
}
