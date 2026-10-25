using System.Security.Claims;
using Chronicles.Api.Data;
using Chronicles.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public sealed class CampaignRoomHub(AppDbContext db, RoomRegistry registry) : Hub
{
    public static string Group(Guid campaignId) => $"campaign:{campaignId:N}";

    public async Task<IReadOnlyList<RoomPeer>> JoinCampaign(Guid campaignId, string mode)
    {
        var userId = UserId();
        if (userId is null || mode is not ("room" or "obs")) throw new HubException("Acesso à sala negado.");
        var member = await db.CampaignMemberships.AsNoTracking().Where(x => x.CampaignId == campaignId && x.UserId == userId)
            .Select(x => new { x.Role, x.User.DisplayName }).SingleOrDefaultAsync();
        if (member is null) throw new HubException("Acesso à sala negado.");
        var previous = registry.Remove(Context.ConnectionId);
        if (previous is not null) await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(previous.CampaignId));
        var peer = new RoomPeer(Context.ConnectionId, campaignId, userId.Value, member.DisplayName, member.Role, mode);
        var existing = registry.Join(peer);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(campaignId));
        await Clients.OthersInGroup(Group(campaignId)).SendAsync("PeerJoined", peer);
        return existing;
    }

    public async Task SetMediaState(bool cameraEnabled, bool screenEnabled)
    {
        var peer = registry.Get(Context.ConnectionId) ?? throw new HubException("Entre na sala primeiro.");
        if (!await Member(peer.CampaignId, peer.UserId)) throw new HubException("Acesso à sala negado.");
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
        if (!await Member(campaignId, peer.UserId) || !await Member(campaignId, target.UserId))
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
    private Task<bool> Member(Guid campaignId, Guid userId) => db.CampaignMemberships.AnyAsync(x => x.CampaignId == campaignId && x.UserId == userId);
}
