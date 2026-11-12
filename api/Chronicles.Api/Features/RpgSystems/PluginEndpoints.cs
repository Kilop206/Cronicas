using System.Security.Claims;
using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static class PluginEndpoints
{
    public static void MapRpgPluginEndpoints(this RouteGroupBuilder api)
    {
        var v1 = api.MapGroup("/v1").WithTags("RPG plugins v1");
        v1.MapGet("/rpg-plugins", (HttpContext http, RpgPluginRegistry registry) =>
            http.User.Identity?.IsAuthenticated == true ? Results.Ok(registry.Plugins) : Results.Unauthorized())
            .WithName("ListRpgPluginsV1").Produces<RPGSystemPlugin[]>().Produces(401);
        v1.MapGet("/characters/{id:guid}/sheet", async Task<IResult> (Guid id, HttpContext http, ReadCharacter query) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Unauthorized();
            var result = await query.Sheet(id, actor, http.RequestAborted);
            if (result is null) return Results.NotFound();
            http.Response.Headers.ETag = $"\"{result.Revision}\"";
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(result);
        }).WithName("ReadCharacterSheetV1").Produces<CharacterSheetEnvelope>().Produces(401).Produces(404);
        v1.MapPut("/characters/{id:guid}/sheet", async Task<IResult> (Guid id, VersionedCharacterInput input,
            HttpContext http, SaveCharacter command, RpgPluginRegistry registry) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Results.Unauthorized();
            var response = await command.Execute(id, actor, input.ToState(), requireRevision: true, ct: http.RequestAborted);
            if (CharacterWriteHttpResult.Error(response) is { } error) return error;
            var envelope = CharacterSheetProjection.Envelope(response.Character!, actor, registry);
            http.Response.Headers.ETag = $"\"{envelope.Revision}\"";
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(envelope);
        }).WithName("SaveCharacterSheetV1").Produces<CharacterSheetEnvelope>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
    }
}
