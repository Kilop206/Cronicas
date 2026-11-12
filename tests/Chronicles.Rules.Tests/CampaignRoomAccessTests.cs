using Chronicles.Api.Services;
using Chronicles.Api;
using Microsoft.AspNetCore.SignalR;

public static class CampaignRoomAccessTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Campaign room access: " + label); }
        var registry = new RoomRegistry(); var campaign = Guid.NewGuid(); var user = Guid.NewGuid();
        var firstMembership = DateTime.UtcNow; var secondMembership = firstMembership.AddMinutes(1);
        RoomPeer Peer(string connection, string role, Guid? otherCampaign = null) => new(connection, otherCampaign ?? campaign, user, "Member", role, "room");
        registry.Join(Peer("old-player", "player"), Guid.NewGuid(), firstMembership);
        registry.Join(Peer("fresh-spectator", "spectator"), Guid.NewGuid(), firstMembership);
        var other = Guid.NewGuid(); registry.Join(Peer("other-campaign", "player", other), Guid.NewGuid(), firstMembership);
        Check(registry.RemoveStaleUser(campaign, user, "spectator", firstMembership).Single().ConnectionId == "old-player", "role reconciliation removes stale authority only");
        Check(registry.Get("fresh-spectator") is not null && registry.Get("other-campaign") is not null, "fresh connection and other campaign are preserved");
        Check(registry.RemoveStaleUser(campaign, user, "spectator", firstMembership).Count == 0, "duplicate delivery is idempotent");
        registry.Join(Peer("rejoined", "spectator"), Guid.NewGuid(), secondMembership);
        Check(registry.RemoveStaleUser(campaign, user, "spectator", secondMembership).Single().ConnectionId == "fresh-spectator", "same role after leave/rejoin invalidates old membership incarnation");
        Check(registry.Get("rejoined") is not null, "delayed event does not disconnect a fresh rejoin");
        Check(registry.RemoveStaleUser(campaign, user, null, null).Single().ConnectionId == "rejoined" && registry.SessionFor("rejoined") is null,
            "removal clears all live authority in the campaign");
        var hub = new TestHub(); var rooms = new RoomAccessService(registry, hub);
        registry.Join(Peer("retry", "player"), Guid.NewGuid(), firstMembership);
        try { await rooms.ReconcileCampaignMember(campaign, user, "spectator", firstMembership); throw new Exception("Expected transport failure."); }
        catch (InvalidOperationException error) when (error.Message == "Simulated transport failure") { }
        Check(registry.Get("retry") is null && hub.GroupManager.Removals == 1, "transport failure cannot restore registry authority");
        await rooms.ReconcileCampaignMember(campaign, user, "spectator", firstMembership);
        Check(hub.GroupManager.Removals == 2 && hub.ClientsRecorder.Messages.Count(m => m == "MembershipChanged") == 1,
            "retry completes group removal/notification even though registry entry was already removed");
        await rooms.ReconcileCampaignMember(campaign, user, "spectator", firstMembership);
        Check(hub.GroupManager.Removals == 2, "successful transport retry is not repeated");
        hub.GroupManager.Failures = 1; registry.Join(Peer("fresh-after-failure", "player"), Guid.NewGuid(), firstMembership);
        try { await rooms.ReconcileCampaignMember(campaign, user, "spectator", firstMembership); throw new Exception("Expected transport failure."); }
        catch (InvalidOperationException error) when (error.Message == "Simulated transport failure") { }
        registry.Join(Peer("fresh-after-failure", "spectator"), Guid.NewGuid(), secondMembership);
        var removals = hub.GroupManager.Removals;
        await rooms.ReconcileCampaignMember(campaign, user, "spectator", secondMembership);
        Check(registry.Get("fresh-after-failure") is not null && hub.GroupManager.Removals == removals,
            "queued transport failure does not remove a freshly reauthenticated membership");
        Console.WriteLine("Campaign room access: role/membership reconciliation, duplicate/delayed delivery, transport retries after registry removal, fresh rejoin and campaign/session isolation passed.");
    }

    private sealed class TestHub : IHubContext<CampaignRoomHub>
    {
        public TestGroups GroupManager = new(); public TestClients ClientsRecorder = new();
        public IHubClients Clients => ClientsRecorder; public IGroupManager Groups => GroupManager;
    }
    private sealed class TestGroups : IGroupManager
    {
        public int Failures = 1, Removals;
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        { Removals++; if (Failures-- > 0) throw new InvalidOperationException("Simulated transport failure"); return Task.CompletedTask; }
    }
    private sealed class TestClients : IHubClients, IClientProxy
    {
        public List<string> Messages = [];
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default) { Messages.Add(method); return Task.CompletedTask; }
        public IClientProxy All => this;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;
        public IClientProxy Client(string connectionId) => this;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => this;
        public IClientProxy Group(string groupName) => this;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => this;
        public IClientProxy User(string userId) => this;
        public IClientProxy Users(IReadOnlyList<string> userIds) => this;
    }
}
