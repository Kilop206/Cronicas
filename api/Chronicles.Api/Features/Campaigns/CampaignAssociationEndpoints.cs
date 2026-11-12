using System.Security.Claims;
using Chronicles.Application.Campaigns;

namespace Chronicles.Api;

public static class CampaignAssociationEndpoints
{
    public static void MapCampaignAssociationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPatch("/campaigns/{campaignId:guid}/system", async Task<IResult> (Guid campaignId, SystemLinkInput input, HttpContext http, ManageCampaignAssociations command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CampaignHttpResult.Error(await command.System(campaignId, actor, input.SystemId, http.RequestAborted)) ?? Results.NoContent();
        }).Produces(204).Produces(401).Produces(404).Produces(409).ProducesValidationProblem();
        api.MapPatch("/campaigns/{campaignId:guid}/world", async Task<IResult> (Guid campaignId, WorldLinkInput input, HttpContext http, ManageCampaignAssociations command) =>
        {
            if (UserId(http) is not { } actor) return Results.Unauthorized();
            return CampaignHttpResult.Error(await command.World(campaignId, actor, input.WorldId, http.RequestAborted)) ?? Results.NoContent();
        }).Produces(204).Produces(401).Produces(404).Produces(409).ProducesValidationProblem();
    }
    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record SystemLinkInput(Guid? SystemId);
    public sealed record WorldLinkInput(Guid? WorldId);
}
