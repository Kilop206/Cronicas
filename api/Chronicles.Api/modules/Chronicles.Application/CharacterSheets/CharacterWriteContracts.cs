using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterCreationContext(Guid CampaignId, string Role, Guid? SystemId, SystemDefinition? System);
public enum CharacterWriteFailure { NotFound, Forbidden, Conflict, Invalid }
public sealed record CharacterWriteResponse(CharacterSheetSnapshot? Character = null, CharacterWriteFailure? Failure = null,
    string? Message = null, Dictionary<string, string[]>? Errors = null);

public interface ICharacterSheetReader
{
    Task<CharacterSheetSnapshot?> Load(Guid characterId, Guid actorId, CancellationToken cancellationToken);
}

public interface ICharacterWriteStore : ICharacterSheetReader
{
    Task<CharacterCreationContext?> LoadCreationContext(Guid campaignId, Guid actorId, CancellationToken cancellationToken);
    Task<CharacterWriteResponse> Create(CharacterCreationContext context, CharacterSheetState state, Guid actorId, CancellationToken cancellationToken);
    Task<CharacterWriteResponse> Save(CharacterSheetSnapshot before, CharacterSheetState state, Guid actorId, CancellationToken cancellationToken);
}
