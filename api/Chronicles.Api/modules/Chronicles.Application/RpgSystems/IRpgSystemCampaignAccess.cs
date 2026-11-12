namespace Chronicles.Application.RpgSystems;

// Public registry contract. In the modular monolith, an owned reference must remain valid until the caller commits.
public interface IRpgSystemCampaignAccess
{
    Task<bool> HoldOwnedReference(Guid system, Guid actor, CancellationToken ct);
}
