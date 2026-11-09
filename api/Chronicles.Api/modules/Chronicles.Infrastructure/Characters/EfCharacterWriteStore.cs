using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterWriteStore(AppDbContext db) : ICharacterWriteStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid id, Guid actor, CancellationToken ct) => CharacterSheetPersistence.Load(db, id, actor, ct);

    public async Task<CharacterCreationContext?> LoadCreationContext(Guid campaignId, Guid actorId, CancellationToken ct)
    {
        var row = await db.CampaignMemberships.AsNoTracking().Where(m => m.CampaignId == campaignId && m.UserId == actorId)
            .Select(m => new { m.Role, m.Campaign.SystemId, Schema = m.Campaign.System == null ? null : m.Campaign.System.SchemaJson }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(campaignId, row.Role, row.SystemId, row.Schema is null ? null : SystemDefinition.Read(row.Schema));
    }

    public async Task<CharacterWriteResponse> Create(CharacterCreationContext before, CharacterSheetState state, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try
        {
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, actor, before.SystemId, ct);
            var fresh = await LoadCreationContext(before.CampaignId, actor, ct);
            if (fresh is null) return new(Failure: CharacterWriteFailure.NotFound);
            if (fresh.Role == "spectator") return new(Failure: CharacterWriteFailure.Forbidden, Message: "Espectadores não podem criar personagens.");
            if (fresh.Role != before.Role || !CharacterSheetPersistence.SameSystem(before.SystemId, before.System, fresh.SystemId, fresh.System)) return Conflict();
            var character = new Character { CampaignId = fresh.CampaignId, OwnerId = actor, Name = state.Name };
            CharacterSheetPersistence.Apply(character, state);
            db.Characters.Add(character);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(CharacterSheetPersistence.Snapshot(character, fresh.Role, fresh.System, fresh.SystemId));
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error)) { db.ChangeTracker.Clear(); return Conflict(); }
    }

    public async Task<CharacterWriteResponse> Save(CharacterSheetSnapshot before, CharacterSheetState state, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try
        {
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, actor, before.SystemId, ct);
            var fresh = await Load(before.Id, actor, ct);
            if (fresh is null) return new(Failure: CharacterWriteFailure.NotFound);
            if (!CharacterAccessPolicy.CanEdit(fresh.OwnerId, actor, fresh.Role))
                return new(Failure: CharacterWriteFailure.Forbidden, Message: "Você não pode editar este personagem.");
            if (fresh.CampaignId != before.CampaignId || fresh.State.Version != before.State.Version || fresh.Role != before.Role ||
                !CharacterSheetPersistence.SameSystem(before.SystemId, before.System, fresh.SystemId, fresh.System)) return Conflict();
            var character = await db.Characters.SingleAsync(c => c.Id == before.Id, ct);
            // Preserve the legacy PUT contract: every accepted save creates a history revision.
            CharacterHistory.Capture(db, character, actor);
            CharacterSheetPersistence.Apply(character, state);
            if (fresh.System is not null) await CharacterEncounterConditions.Sync(db, character, fresh.System);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(CharacterSheetPersistence.Snapshot(character, fresh.Role, fresh.System, fresh.SystemId));
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error)) { db.ChangeTracker.Clear(); return Conflict(); }
        catch (ArgumentException error)
        {
            db.ChangeTracker.Clear();
            return new(Failure: CharacterWriteFailure.Invalid, Errors: new() { ["rules"] = [error.Message] });
        }
    }

    private static CharacterWriteResponse Conflict() => new(Failure: CharacterWriteFailure.Conflict,
        Message: "A ficha ou o sistema mudou. Atualize antes de salvar.");
}
