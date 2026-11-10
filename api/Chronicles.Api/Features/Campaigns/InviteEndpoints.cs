using System.Security.Claims;
using Chronicles.Application.Campaigns;
using Microsoft.AspNetCore.RateLimiting;

namespace Chronicles.Api;

public static class InviteEndpoints
{
    public static void MapInviteEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/campaigns/{id:guid}/invites", async Task<IResult> (Guid id, InviteRequest input, HttpContext http, ManageCampaignInvites command) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var result = await command.Create(id, userId, input.Role, http.RequestAborted);
            return CampaignHttpResult.Error(result) ?? Results.Created($"/api/invites/{result.Value!.Token}", result.Value);
        }).RequireRateLimiting("account").Produces<CampaignInviteDocument>(201).Produces(401).Produces(403).Produces(404).Produces(409).ProducesValidationProblem();

        api.MapGet("/invites/{token}", async Task<IResult> (string token, HttpContext http, ManageCampaignInvites query) =>
        {
            if (UserId(http) is null) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var result = await query.Preview(token, http.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).Produces<CampaignInvitePreview>().Produces(401).Produces(404);

        api.MapPost("/invites/{token}/accept", async Task<IResult> (string token, HttpContext http, ManageCampaignInvites command) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            http.Response.Headers.CacheControl = "private, no-store";
            var result = await command.Accept(token, userId, http.RequestAborted);
            return CampaignHttpResult.Error(result) ?? Results.Ok(result.Value);
        }).RequireRateLimiting("account").Produces<CampaignJoined>().Produces(401).Produces(404).Produces(409);
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record InviteRequest(string Role);
}
