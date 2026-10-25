using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Api.Services;
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

        api.MapPatch("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, RoleInput input, HttpContext http, AppDbContext db, RealtimeKitService realtime) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == userId);
            if (campaign is null) return Results.NotFound();
            if (memberId == userId || input.Role is not ("gm" or "co-gm" or "player" or "spectator")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Papel inválido."] });
            var member = await db.CampaignMemberships.SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == memberId);
            if (member is null) return Results.NotFound();
            if (member.Role == input.Role) return Results.NoContent();
            var failure = await RevokeRoomParticipant(db, realtime, campaign, memberId, http.RequestAborted);
            if (failure is not null) return failure;
            member.Role = input.Role;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/campaigns/{campaignId:guid}/members/{memberId:guid}", async Task<IResult> (Guid campaignId, Guid memberId, HttpContext http, AppDbContext db, RealtimeKitService realtime) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campaignId);
            if (campaign is null || !await db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId)) return Results.NotFound();
            if (memberId == campaign.OwnerId || userId != campaign.OwnerId && userId != memberId) return Results.Problem("Sem permissão para remover este participante.", statusCode: 403);
            var member = await db.CampaignMemberships.SingleOrDefaultAsync(x => x.CampaignId == campaignId && x.UserId == memberId);
            if (member is null) return Results.NotFound();
            var failure = await RevokeRoomParticipant(db, realtime, campaign, memberId, http.RequestAborted);
            if (failure is not null) return failure;
            db.CampaignMemberships.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> RevokeRoomParticipant(AppDbContext db, RealtimeKitService realtime, Campaign campaign, Guid memberId, CancellationToken cancellationToken)
    {
        var participant = await db.RoomParticipants.SingleOrDefaultAsync(x => x.CampaignId == campaign.Id && x.UserId == memberId, cancellationToken);
        if (participant is null) return null;
        if (campaign.RealtimeMeetingId is null || !realtime.IsConfigured)
            return Results.Problem("Não foi possível revogar o acesso à sala de mídia agora.", statusCode: 503);
        try { await realtime.RevokeParticipantAsync(campaign.RealtimeMeetingId.Value, participant.RealtimeParticipantId, memberId, cancellationToken); }
        catch (HttpRequestException) { return Results.Problem("A revogação de mídia falhou. Tente novamente.", statusCode: 502); }
        db.RoomParticipants.Remove(participant);
        return null;
    }
    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public sealed record RoleInput(string Role);
}
