using Chronicles.Api.Data;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterDeletionStore(AppDbContext db) : ICharacterDeletionStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid id,Guid actor,CancellationToken ct)=>CharacterSheetPersistence.Load(db,id,actor,ct);
    public async Task<CharacterWriteResponse> Delete(CharacterSheetSnapshot before,Guid actor,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        try
        {
            await CharacterSheetPersistence.LockContext(db,before.CampaignId,actor,null,ct);
            var row=await db.Characters.FromSqlInterpolated($"SELECT * FROM characters WHERE \"Id\" = {before.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
            var fresh=await Load(before.Id,actor,ct);
            if(row is null||fresh is null||fresh.Role=="spectator")return new(Failure:CharacterWriteFailure.NotFound);
            if(!CharacterAccessPolicy.CanEdit(fresh.OwnerId,actor,fresh.Role))return new(Failure:CharacterWriteFailure.Forbidden,Message:"Você não pode excluir esta ficha.");
            if(fresh.State.Version!=before.State.Version||fresh.CampaignId!=before.CampaignId||fresh.OwnerId!=before.OwnerId||fresh.Role!=before.Role)return Conflict();
            if(await db.EncounterParticipants.AnyAsync(p=>p.CharacterId==before.Id,ct))return Linked();
            // Database cascades remove private notes, history, inventory, abilities and favorites.
            // SaveChanges stages CharacterDeleted in the same transaction as deletion.
            db.Characters.Remove(row);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            return new();
        }
        catch(Exception error) when(CharacterSheetPersistence.IsConflict(error)){db.ChangeTracker.Clear();return Conflict();}
        catch(DbUpdateException error) when(error.InnerException is PostgresException { SqlState:PostgresErrorCodes.ForeignKeyViolation }){db.ChangeTracker.Clear();return Linked();}
    }
    private static CharacterWriteResponse Conflict()=>new(Failure:CharacterWriteFailure.Conflict,Message:"A ficha mudou. Recarregue e confirme novamente antes de excluir.");
    private static CharacterWriteResponse Linked()=>new(Failure:CharacterWriteFailure.Conflict,Message:"Remova esta ficha dos encontros da campanha antes de excluir. A exclusão foi cancelada.");
}
