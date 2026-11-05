using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterRuleSnapshot(Guid Id, Guid CampaignId, Guid OwnerId, string Role,
    CharacterSheetState State, SystemDefinition? System, DateTime UpdatedAt, Guid? SystemId);
public sealed record CharacterRuleChange(CharacterRuleSnapshot Before, RuleResult Result, Guid ActorId, string Visibility);
public enum RuleFailure { NotFound, Forbidden, Conflict, Invalid }
public sealed record RuleCommandResponse(CharacterRuleSnapshot? Character = null, RuleResult? Result = null,
    RuleFailure? Failure = null, string? Message = null, Dictionary<string, string[]>? Errors = null);

// A port for this use case, not a generic repository or a cross-module EF abstraction.
public interface ICharacterRuleStore
{
    Task<CharacterRuleSnapshot?> Load(Guid characterId, Guid actorId, CancellationToken cancellationToken);
    Task<CharacterRuleSnapshot?> Commit(CharacterRuleChange change, CancellationToken cancellationToken);
}

public interface IGameRandom
{
    int Draw(int sides);
}
