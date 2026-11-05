using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

// v1 read contract. Revisions/system metadata describe the effective campaign template;
// old sheets do not gain invented creation dates or silently migrate their fields.
public sealed record CharacterSheetEnvelope(int ContractVersion, Guid CharacterId, Guid OwnerId, Guid CampaignId,
    Guid? RegistrySystemId, string? SystemId, string? SystemVersion, int SchemaVersion, int TemplateRevision,
    int Revision, DateTime UpdatedAt, CharacterSheetState State, string RendererId, bool CanEdit, bool RulesAvailable);
