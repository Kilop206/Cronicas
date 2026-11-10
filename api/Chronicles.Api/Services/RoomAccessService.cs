using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Chronicles.Api.Services;

public sealed class RoomAccessService(RoomRegistry registry, IHubContext<CampaignRoomHub> hub)
{
    private sealed record MembershipRemoval(RoomPeer Peer, Guid Generation);
    private readonly ConcurrentDictionary<string, MembershipRemoval> membershipRemovals = new(StringComparer.Ordinal);
    public Task RevokeSession(Guid sessionId) => Disconnect(registry.RemoveSession(sessionId));
    public Task RevokeUser(Guid userId) => Disconnect(registry.RemoveAllUser(userId));
    public Task RevokeCampaignMember(Guid campaignId, Guid userId) => Disconnect(registry.RemoveUser(campaignId, userId));
    public async Task ReconcileCampaignMember(Guid campaignId, Guid userId, string? role, DateTime? joinedAt)
    {
        foreach (var peer in registry.RemoveStaleUser(campaignId, userId, role, joinedAt))
            membershipRemovals[peer.ConnectionId] = new(peer, Guid.NewGuid());
        foreach (var pending in membershipRemovals.Where(p => p.Value.Peer.CampaignId == campaignId && p.Value.Peer.UserId == userId).ToArray())
        {
            // Keep a failed transport removal until outbox delivery retries, even after registry authority was removed.
            // A connection that authenticated/joined again has a fresh registry entry and must not replay the old removal.
            if (registry.Get(pending.Key) is null) await Disconnect([pending.Value.Peer]);
            ((ICollection<KeyValuePair<string, MembershipRemoval>>)membershipRemovals).Remove(pending);
        }
    }

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
