using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class ReadCharacterLifecycle(ICharacterLifecycleStore store)
{
    public async Task<CharacterDocument[]?> Campaign(Guid campaignId, Guid actor, CancellationToken ct = default)
    {
        var listing = await store.ListCampaign(campaignId, actor, ct);
        return listing?.Characters.Select(c => CharacterSheetProjection.Document(c, listing.Role, actor)).ToArray();
    }

    public async Task<CharacterRevisionSummary[]?> History(Guid id, Guid actor, CancellationToken ct = default)
    {
        var current = await store.Load(id, actor, ct);
        if (current is null || !CharacterAccessPolicy.CanEdit(current.OwnerId, actor, current.Role)) return null;
        return await store.ListRevisions(id, actor, 100, ct);
    }
}
