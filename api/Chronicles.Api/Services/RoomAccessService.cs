using Microsoft.AspNetCore.SignalR;

namespace Chronicles.Api.Services;

public sealed class RoomAccessService(RoomRegistry registry, IHubContext<CampaignRoomHub> hub)
{
    public Task RevokeSession(Guid sessionId) => Disconnect(registry.RemoveSession(sessionId));
    public Task RevokeUser(Guid userId) => Disconnect(registry.RemoveAllUser(userId));
    public Task RevokeCampaignMember(Guid campaignId, Guid userId) => Disconnect(registry.RemoveUser(campaignId, userId));

    private async Task Disconnect(IReadOnlyList<RoomPeer> peers)
    {
        foreach (var peer in peers)
        {
            await hub.Groups.RemoveFromGroupAsync(peer.ConnectionId, CampaignRoomHub.Group(peer.CampaignId));
            await hub.Clients.Client(peer.ConnectionId).SendAsync("MembershipChanged");
            await hub.Clients.Group(CampaignRoomHub.Group(peer.CampaignId)).SendAsync("PeerLeft", peer.ConnectionId);
        }
    }
}
