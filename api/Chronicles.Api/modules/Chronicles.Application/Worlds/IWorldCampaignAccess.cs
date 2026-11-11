namespace Chronicles.Application.Worlds;

// Public world contract, participating in the same local unit of work as the campaign association.
public interface IWorldCampaignAccess
{
    Task<bool> HoldOwnedReference(Guid world, Guid actor, CancellationToken ct);
}
