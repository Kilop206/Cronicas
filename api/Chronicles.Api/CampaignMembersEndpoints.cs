using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class CampaignMembersEndpoints
{
    public static void MapCampaignMembersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campaigns/{campaignId:guid}/members", async Task<IResult> (Guid campaignId, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId)) return Results.NotFound();
            var members = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId)
                .OrderBy(x => x.JoinedAt).Select(x => new { x.UserId, x.User.DisplayName, x.Role, x.JoinedAt }).ToListAsync();
            return Results.Ok(members);
        });

        api.MapPatch("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, RoleInput input, HttpContext http, AppDbContext db, RoomRegistry rooms, IHubContext<CampaignRoomHub> hub) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            if (!await db.Campaigns.AnyAsync(x => x.Id == campaignId && x.OwnerId == userId)) return Results.NotFound();
            if (memberId == userId || input.Role is not ("gm" or "co-gm" or "player" or "spectator"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Papel inválido."] });
            var member = await db.CampaignMemberships.SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == memberId);
            if (member is null) return Results.NotFound();
            if (member.Role == input.Role) return Results.NoContent();
            member.Role = input.Role;
            await db.SaveChangesAsync();
            await RevokeRoomAccess(rooms, hub, campaignId, memberId);
            return Results.NoContent();
        });

        api.MapDelete("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, HttpContext http, AppDbContext db, RoomRegistry rooms, IHubContext<CampaignRoomHub> hub) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campaignId);
            if (campaign is null || !await db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId)) return Results.NotFound();
            if (memberId == campaign.OwnerId || userId != campaign.OwnerId && userId != memberId)
                return Results.Problem("Sem permissão para remover este participante.", statusCode: 403);
            var member = await db.CampaignMemberships.SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == memberId);
            if (member is null) return Results.NotFound();
            db.CampaignMemberships.Remove(member);
            await db.SaveChangesAsync();
            await RevokeRoomAccess(rooms, hub, campaignId, memberId);
            return Results.NoContent();
        });
    }

    private static async Task RevokeRoomAccess(RoomRegistry rooms, IHubContext<CampaignRoomHub> hub, Guid campaignId, Guid memberId)
    {
        foreach (var peer in rooms.RemoveUser(campaignId, memberId))
        {
            await hub.Groups.RemoveFromGroupAsync(peer.ConnectionId, CampaignRoomHub.Group(campaignId));
            await hub.Clients.Client(peer.ConnectionId).SendAsync("MembershipChanged");
            await hub.Clients.Group(CampaignRoomHub.Group(campaignId)).SendAsync("PeerLeft", peer.ConnectionId);
        }
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record RoleInput(string Role);
}
