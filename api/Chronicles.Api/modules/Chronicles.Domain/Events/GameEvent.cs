namespace Chronicles.Domain.Events;

public static class GameEventTypes
{
    public const string CharacterCreated = "CharacterCreated";
    public const string CharacterUpdated = "CharacterUpdated";
    public const string CharacterDeleted = "CharacterDeleted";
    public const string CharacterSheetUpdated = "CharacterSheetUpdated";
    public const string CampaignCreated = "CampaignCreated";
    public const string PlayerJoinedCampaign = "PlayerJoinedCampaign";
    public const string DiceRolled = "DiceRolled";
    public const string RPGSystemRegistered = "RPGSystemRegistered";
}

// Integration-safe invalidation envelope. Never carries fields, secrets, roll results or session tokens.
public sealed record GameEvent(Guid EventId, string Type, int ContractVersion, DateTime OccurredAt,
    Guid AggregateId, Guid? CampaignId, int? Revision, string? CorrelationId);
