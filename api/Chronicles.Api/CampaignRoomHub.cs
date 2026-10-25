using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public sealed class CampaignRoomHub(AppDbContext db, RoomRegistry registry) : Hub
{
    public static string Group(Guid campaignId) => $"campaign:{campaignId:N}";

    public async Task<IReadOnlyList<RoomPeer>> JoinCampaign(Guid campaignId, string mode, Guid? characterId)
    {
        var userId = UserId();
        var sessionId = Context.GetHttpContext()?.Items["sessionId"] is Guid id ? id : (Guid?)null;
        if (userId is null || sessionId is null || !await ActiveSession(sessionId.Value) || mode is not ("room" or "obs"))
            throw new HubException("Acesso à sala negado.");
        var member = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId)
            .Select(x => new { x.Role, x.User.DisplayName }).SingleOrDefaultAsync();
        if (member is null) throw new HubException("Acesso à sala negado.");
        Guid? targetUserId = null;
        if (mode == "obs")
        {
            targetUserId = await db.Characters.AsNoTracking().Where(x => x.Id == characterId && x.CampaignId == campaignId)
                .Select(x => (Guid?)x.OwnerId).SingleOrDefaultAsync();
            if (targetUserId is null) throw new HubException("Personagem indisponível para esta fonte.");
        }
        else if (characterId is not null) throw new HubException("Destino de mídia inválido.");
        var previous = registry.Remove(Context.ConnectionId);
        if (previous is not null) await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(previous.CampaignId));
        var peer = new RoomPeer(Context.ConnectionId, campaignId, userId.Value, member.DisplayName, member.Role, mode, TargetUserId: targetUserId);
        var existing = registry.Join(peer, sessionId.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(campaignId));
        foreach (var other in existing) await Clients.Client(other.ConnectionId).SendAsync("PeerJoined", peer);
        return existing;
    }

    public async Task SetMediaState(bool cameraEnabled, bool screenEnabled)
    {
        var peer = registry.Get(Context.ConnectionId) ?? throw new HubException("Entre na sala primeiro.");
        if (!await Member(peer.CampaignId, peer.UserId) || registry.SessionFor(Context.ConnectionId) is not { } sessionId || !await ActiveSession(sessionId))
            throw new HubException("Acesso à sala negado.");
        if (peer.Mode == "obs" || peer.Role == "spectator" && (cameraEnabled || screenEnabled))
            throw new HubException("Este papel não pode publicar mídia.");
        var updated = registry.Update(Context.ConnectionId, cameraEnabled, screenEnabled);
        if (updated is not null) await Clients.OthersInGroup(Group(peer.CampaignId)).SendAsync("PeerState", updated);
    }

    public async Task SendSignal(Guid campaignId, string targetConnectionId, string kind, string payload)
    {
        if (kind is not ("offer" or "answer" or "ice") || payload is null || payload.Length > (kind == "ice" ? 4096 : 16000))
            throw new HubException("Sinal inválido.");
        var peer = registry.Get(Context.ConnectionId);
        var target = registry.Get(targetConnectionId);
        if (peer is null || target is null || peer.CampaignId != campaignId || !registry.SameCampaign(Context.ConnectionId, targetConnectionId, campaignId))
            throw new HubException("Destino indisponível.");
        if (!registry.AllowSignal(Context.ConnectionId)) throw new HubException("Limite de sinalização excedido.");
        if (registry.SessionFor(Context.ConnectionId) is not { } sourceSession || registry.SessionFor(targetConnectionId) is not { } targetSession
            || !await ActiveSession(sourceSession) || !await ActiveSession(targetSession)
            || !await Member(campaignId, peer.UserId) || !await Member(campaignId, target.UserId))
            throw new HubException("Destino indisponível.");
        await Clients.Client(targetConnectionId).SendAsync("Signal", Context.ConnectionId, kind, payload);
    }

    public async Task LeaveCampaign()
    {
        var peer = registry.Remove(Context.ConnectionId);
        if (peer is null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(peer.CampaignId));
        await Clients.Group(Group(peer.CampaignId)).SendAsync("PeerLeft", peer.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveCampaign();
        await base.OnDisconnectedAsync(exception);
    }

    private Guid? UserId() => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private Task<bool> ActiveSession(Guid sessionId) => db.UserSessions.AnyAsync(x => x.Id == sessionId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);
    private Task<bool> Member(Guid campaignId, Guid userId) => db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId);
}
