using System.Security.Claims;
using Chronicles.Application.Campaigns;

namespace Chronicles.Api;

public static class CampaignMembersEndpoints
{
    public static void MapCampaignMembersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/members", async Task<IResult> (Guid campaignId, HttpContext http, ManageCampaignMembers query) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var result = await query.List(campaignId, userId, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CampaignMemberDocument[]>().Produces(401).Produces(404);

        api.MapPatch("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, RoleInput input, HttpContext http, ManageCampaignMembers command) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            return CampaignHttpResult.Error(await command.Role(campaignId, memberId, userId, input.Role, http.RequestAborted)) ?? Results.NoContent();
        }).Produces(204).Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapDelete("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, HttpContext http, ManageCampaignMembers command) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            return CampaignHttpResult.Error(await command.Remove(campaignId, memberId, userId, http.RequestAborted)) ?? Results.NoContent();
        }).Produces(204).Produces(401).Produces(403).Produces(404).Produces(409);
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record RoleInput(string Role);
}
