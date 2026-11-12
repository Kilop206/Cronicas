using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

// Read projections carry only the information needed for list/history views.
public sealed record CharacterListEntry(Guid Id, Guid CampaignId, Guid OwnerId, CharacterSheetState State, DateTime UpdatedAt, Guid? SystemVersionId = null);
public sealed record CampaignCharacterListing(string Role, CharacterListEntry[] Characters);
public sealed record CharacterRevisionSummary(Guid Id, int Version, DateTime CreatedAt, string EditedBy);
public sealed record CharacterRevisionState(Guid Id, Guid CharacterId, CharacterSheetState State);

public interface ICharacterLifecycleStore : ICharacterSheetReader
{
    Task<CampaignCharacterListing?> ListCampaign(Guid campaignId, Guid actor, CancellationToken ct);
    Task<CharacterRevisionSummary[]?> ListRevisions(Guid characterId, Guid actor, int limit, CancellationToken ct);
    Task<CharacterRevisionState?> LoadRevision(Guid characterId, Guid revisionId, Guid actor, CancellationToken ct);
    Task<CharacterWriteResponse> Duplicate(CharacterSheetSnapshot source, CharacterSheetState copy, Guid actor, CancellationToken ct);
}
