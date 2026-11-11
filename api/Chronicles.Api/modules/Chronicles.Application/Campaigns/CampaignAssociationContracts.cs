using Chronicles.Domain.Campaigns;

namespace Chronicles.Application.Campaigns;

public enum CampaignAssociationKind { System, World }
public sealed record CampaignAssociationChange(CampaignAssociationKind Kind, Guid? TargetId);

public interface ICampaignAssociationStore : ICampaignReader
{
    Task<CampaignResult<bool>> Change(CampaignContext before, Guid actor, CampaignAssociationChange change, CancellationToken ct);
}

public sealed class ManageCampaignAssociations(ICampaignAssociationStore store)
{
    public Task<CampaignResult<bool>> System(Guid campaign, Guid actor, Guid? target, CancellationToken ct = default) =>
        Change(campaign, actor, new(CampaignAssociationKind.System, target), ct);
    public Task<CampaignResult<bool>> World(Guid campaign, Guid actor, Guid? target, CancellationToken ct = default) =>
        Change(campaign, actor, new(CampaignAssociationKind.World, target), ct);

    private async Task<CampaignResult<bool>> Change(Guid campaign, Guid actor, CampaignAssociationChange change, CancellationToken ct)
    {
        var before = await store.Read(campaign, actor, ct);
        // Preserve the owner-scoped legacy 404 contract; membership alone never grants association rights.
        if (before is null || !CampaignPolicy.IsOwner(before.OwnerId, actor, before.Document.Role)) return new(Failure: CampaignFailure.NotFound);
        if (change.TargetId == Guid.Empty) return Unavailable(change.Kind);
        return await store.Change(before, actor, change, ct);
    }

    public static CampaignResult<bool> Unavailable(CampaignAssociationKind kind) => new(Failure: CampaignFailure.Invalid,
        Errors: kind == CampaignAssociationKind.System ? new() { ["systemId"] = ["Sistema indisponível."] } : new() { ["worldId"] = ["Universo indisponível."] });
}
