namespace Chronicles.Api.Services;

public sealed record RoomPeer(string ConnectionId, Guid CampaignId, Guid UserId, string DisplayName, string Role, string Mode,
    bool CameraEnabled = false, bool ScreenEnabled = false, Guid? TargetUserId = null);

public sealed class RoomRegistry
{
    private readonly Dictionary<string, RoomPeer> peers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DateTime Started, int Count)> signalWindows = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public IReadOnlyList<RoomPeer> Join(RoomPeer peer, Guid sessionId)
    {
        lock (gate)
        {
            peers.Remove(peer.ConnectionId);
            sessions.Remove(peer.ConnectionId);
            signalWindows.Remove(peer.ConnectionId);
            var existing = peers.Values.Where(x => CanConnect(x, peer)).ToArray();
            peers.Add(peer.ConnectionId, peer);
            sessions.Add(peer.ConnectionId, sessionId);
            return existing;
        }
    }

    public RoomPeer? Get(string connectionId)
    {
        lock (gate) return peers.GetValueOrDefault(connectionId);
    }

    public Guid? SessionFor(string connectionId)
    {
        lock (gate) return sessions.TryGetValue(connectionId, out var id) ? id : null;
    }

    public bool SameCampaign(string sourceId, string targetId, Guid campaignId)
    {
        lock (gate) return peers.TryGetValue(sourceId, out var source) && source.CampaignId == campaignId
            && peers.TryGetValue(targetId, out var target) && CanConnect(source, target);
    }

    public RoomPeer? Update(string connectionId, bool camera, bool screen)
    {
        lock (gate)
        {
            if (!peers.TryGetValue(connectionId, out var current)) return null;
            var updated = current with { CameraEnabled = camera, ScreenEnabled = screen };
            peers[connectionId] = updated;
            return updated;
        }
    }

    public RoomPeer? Remove(string connectionId)
    {
        lock (gate)
        {
            signalWindows.Remove(connectionId);
            sessions.Remove(connectionId);
            return peers.Remove(connectionId, out var peer) ? peer : null;
        }
    }

    public bool AllowSignal(string connectionId)
    {
        lock (gate)
        {
            if (!peers.ContainsKey(connectionId)) return false;
            var now = DateTime.UtcNow;
            var window = signalWindows.GetValueOrDefault(connectionId);
            if (now - window.Started >= TimeSpan.FromSeconds(10)) window = (now, 0);
            if (window.Count >= 500) return false;
            signalWindows[connectionId] = (window.Started, window.Count + 1);
            return true;
        }
    }

    public IReadOnlyList<RoomPeer> RemoveUser(Guid campaignId, Guid userId)
    {
        lock (gate)
        {
            var removed = peers.Values.Where(x => x.CampaignId == campaignId && x.UserId == userId).ToArray();
            foreach (var peer in removed) RemoveCore(peer.ConnectionId);
            return removed;
        }
    }

    public IReadOnlyList<RoomPeer> RemoveSession(Guid sessionId)
    {
        lock (gate)
        {
            var removed = peers.Values.Where(x => sessions.GetValueOrDefault(x.ConnectionId) == sessionId).ToArray();
            foreach (var peer in removed) RemoveCore(peer.ConnectionId);
            return removed;
        }
    }

    public IReadOnlyList<RoomPeer> RemoveAllUser(Guid userId)
    {
        lock (gate)
        {
            var removed = peers.Values.Where(x => x.UserId == userId).ToArray();
            foreach (var peer in removed) RemoveCore(peer.ConnectionId);
            return removed;
        }
    }

    private void RemoveCore(string connectionId)
    {
        peers.Remove(connectionId);
        sessions.Remove(connectionId);
        signalWindows.Remove(connectionId);
    }

    private static bool CanConnect(RoomPeer first, RoomPeer second)
    {
        if (first.CampaignId != second.CampaignId) return false;
        if (first.Mode == "room" && second.Mode == "room") return true;
        if (first.Mode == "obs") return second.Mode == "room" && first.TargetUserId == second.UserId;
        return second.Mode == "obs" && second.TargetUserId == first.UserId;
    }
}
