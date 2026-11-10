using System.Security.Claims;
using Chronicles.Application.Campaigns;

namespace Chronicles.Api;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns", async Task<IResult> (HttpContext http, ReadCampaigns query, int? page, string? search) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return Results.Ok(await query.List(actor, page, search, http.RequestAborted));
        }).Produces<CampaignPage>().Produces(401);
        api.MapPost("/campaigns", async Task<IResult> (CampaignInput input, HttpContext http, ManageCampaigns command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await command.Create(actor, input.Name, input.Description, input.AccentColor, http.RequestAborted);
            return CampaignHttpResult.Error(result) ?? Results.Created($"/api/campaigns/{result.Value!.Id}", result.Value);
        }).Produces<CampaignDocument>(201).Produces(401).ProducesValidationProblem();
        api.MapGet("/campaigns/{id:guid}", async Task<IResult> (Guid id, HttpContext http, ReadCampaigns query) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await query.Read(id, actor, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CampaignDocument>().Produces(401).Produces(404);
        api.MapPatch("/campaigns/{id:guid}", async Task<IResult> (Guid id, CampaignInput input, HttpContext http, ManageCampaigns command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await command.Edit(id, actor, input.Name, input.Description, input.AccentColor, http.RequestAborted);
            return CampaignHttpResult.Error(result) ?? Results.Ok(result.Value);
        }).Produces<CampaignDocument>().Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
        api.MapPatch("/campaigns/{id:guid}/status", async Task<IResult> (Guid id, StatusInput input, HttpContext http, ManageCampaigns command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            var result = await command.Status(id, actor, input.Status, http.RequestAborted);
            return CampaignHttpResult.Error(result) ?? Results.NoContent();
        }).Produces(204).Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();
    }
    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record CampaignInput(string Name, string Description, string AccentColor);
    public sealed record StatusInput(string Status);
}

internal static class CampaignHttpResult
{
    internal static IResult? Error<T>(CampaignResult<T> result) => result.Failure switch
    {
        CampaignFailure.NotFound => Results.NotFound(),
        CampaignFailure.Forbidden => Results.Problem(result.Message, statusCode: 403),
        CampaignFailure.Invalid => Results.ValidationProblem(result.Errors ?? new() { ["campaign"] = [result.Message ?? "Campanha inválida."] }),
        CampaignFailure.Conflict => Results.Conflict(new { message = result.Message }),
        _ => null
    };
}
