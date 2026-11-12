using Chronicles.Application.Campaigns;

public static class CampaignAssociationTests
{
    public static async Task Run()
    {
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("Campaign associations: " + label); }
        var owner = Guid.NewGuid(); var source = Guid.NewGuid(); var world = Guid.NewGuid();
        var document = new CampaignDocument(Guid.NewGuid(), "Campaign", "", "planning", "#abcdef", "owner", DateTime.UtcNow, source, null);
        var store = new Store(new(document, owner)); var command = new ManageCampaignAssociations(store);
        Check((await command.System(document.Id, owner, Guid.Empty)).Errors?.ContainsKey("systemId") == true && store.Writes == 0, "empty system ID rejected before persistence");
        Check((await command.World(document.Id, owner, Guid.Empty)).Errors?.ContainsKey("worldId") == true && store.Writes == 0, "empty world ID rejected before persistence");
        Check((await command.System(document.Id, owner, source)).Failure is null && store.LastChange == new CampaignAssociationChange(CampaignAssociationKind.System, source), "system command uses typed public port");
        Check((await command.World(document.Id, owner, world)).Failure is null && store.LastChange == new CampaignAssociationChange(CampaignAssociationKind.World, world), "world command uses typed public port");
        Check((await command.System(document.Id, owner, null)).Failure is null && store.LastChange?.TargetId is null, "system detach is explicit and supported");
        Check((await command.World(document.Id, owner, null)).Failure is null && store.LastChange is { Kind: CampaignAssociationKind.World, TargetId: null }, "world detach retains its own context");
        var writes = store.Writes;
        store.Current = new(document with { Role = "co-gm" }, owner);
        Check((await command.System(document.Id, owner, source)).Failure == CampaignFailure.NotFound && store.Writes == writes, "master privileges do not grant owner-only associations");
        store.Current = new(document, Guid.NewGuid());
        Check((await command.World(document.Id, owner, world)).Failure == CampaignFailure.NotFound && store.Writes == writes, "role label alone cannot impersonate real owner");
        store.Current = null;
        Check((await command.System(document.Id, owner, source)).Failure == CampaignFailure.NotFound, "unreadable campaign stays hidden");
        store.Current = new(document, owner); store.Result = new(Failure: CampaignFailure.Conflict);
        Check((await command.World(document.Id, owner, world)).Failure == CampaignFailure.Conflict && store.Writes == writes + 1, "conflicts propagate without automatic overwrites/retries");
        Console.WriteLine("Campaign associations application: owner identity/role, scoped 404, typed ports, empty IDs, detach and conflict propagation passed.");
    }
    private sealed class Store(CampaignContext current) : ICampaignAssociationStore
    {
        public CampaignContext? Current = current; public int Writes; public CampaignAssociationChange? LastChange;
        public CampaignResult<bool> Result = new(true);
        public Task<CampaignContext?> Read(Guid campaign, Guid actor, CancellationToken ct) => Task.FromResult(Current);
        public Task<CampaignResult<bool>> Change(CampaignContext before, Guid actor, CampaignAssociationChange change, CancellationToken ct)
        { Writes++; LastChange = change; return Task.FromResult(Result); }
    }
}
