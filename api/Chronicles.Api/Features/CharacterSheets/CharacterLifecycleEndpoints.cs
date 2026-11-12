using Chronicles.Application.CharacterSheets;

namespace Chronicles.Api;

public static partial class CharacterEndpoints
{
    private static void MapCharacterLifecycle(RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/characters", async Task<IResult> (Guid campaignId, HttpContext http, ReadCharacterLifecycle query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var items = await query.Campaign(campaignId, actor, http.RequestAborted);
            return items is null ? Results.NotFound() : Results.Ok(items);
        }).Produces<CharacterDocument[]>().Produces(401).Produces(404);

        api.MapGet("/characters/{id:guid}/revisions", async Task<IResult> (Guid id, HttpContext http, ReadCharacterLifecycle query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var revisions = await query.History(id, actor, http.RequestAborted);
            return revisions is null ? Results.NotFound() : Results.Ok(revisions);
        }).Produces<CharacterRevisionSummary[]>().Produces(401).Produces(404);

        api.MapPost("/characters/{id:guid}/duplicate", async Task<IResult> (Guid id, HttpContext http, DuplicateCharacter command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CharacterWriteHttpResult.Legacy(await command.Execute(id, actor, http.RequestAborted), actor, created: true);
        }).Produces<CharacterDocument>(201).Produces(401).Produces(403).Produces(404).Produces(409);

        api.MapPost("/characters/{id:guid}/revisions/{revisionId:guid}/restore", async Task<IResult> (Guid id, Guid revisionId,
            RestoreInput input, HttpContext http, RestoreCharacter command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CharacterWriteHttpResult.Legacy(await command.Execute(id, revisionId, input.ExpectedVersion, actor, http.RequestAborted), actor);
        }).Produces<CharacterDocument>().Produces(401).Produces(404).Produces(409).ProducesValidationProblem();
    }

    public sealed record RestoreInput(int ExpectedVersion);
}
