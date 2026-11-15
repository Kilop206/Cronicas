using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public interface ICharacterDeletionStore : ICharacterSheetReader
{
    Task<CharacterWriteResponse> Delete(CharacterSheetSnapshot before, Guid actor, CancellationToken ct);
}

public sealed class DeleteCharacter(ICharacterDeletionStore store)
{
    public async Task<CharacterWriteResponse> Execute(Guid id, int? expectedVersion, Guid actor, CancellationToken ct = default)
    {
        var current=await store.Load(id,actor,ct);
        if(current is null||current.Role=="spectator")return new(Failure:CharacterWriteFailure.NotFound);
        if(!CharacterAccessPolicy.CanEdit(current.OwnerId,actor,current.Role))return new(Failure:CharacterWriteFailure.Forbidden,Message:"Você não pode excluir esta ficha.");
        if(expectedVersion is null or <1)return new(Failure:CharacterWriteFailure.Invalid,Errors:new(){["version"]=["Informe a versão da ficha confirmada para exclusão."]});
        if(current.State.Version!=expectedVersion)return new(Failure:CharacterWriteFailure.Conflict,Message:"A ficha mudou. Recarregue e confirme novamente antes de excluir.");
        return await store.Delete(current,actor,ct);
    }
}
