using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterLifecycleStore(AppDbContext db) : ICharacterLifecycleStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid id, Guid actor, CancellationToken ct) => CharacterSheetPersistence.Load(db, id, actor, ct);

    public async Task<CampaignCharacterListing?> ListCampaign(Guid campaignId, Guid actor, CancellationToken ct)
    {
        var role = await db.CampaignMemberships.AsNoTracking().Where(m => m.CampaignId == campaignId && m.UserId == actor)
            .Select(m => m.Role).SingleOrDefaultAsync(ct);
        if (role is null) return null;
        var characters = await db.Characters.AsNoTracking().Where(c => c.CampaignId == campaignId && c.Campaign.Memberships.Any(m => m.UserId == actor))
            .OrderBy(c => c.Name).ToListAsync(ct);
        return new(role, characters.Select(c => new CharacterListEntry(c.Id, c.CampaignId, c.OwnerId, CharacterSheetPersistence.State(c), c.UpdatedAt, c.SystemVersionId)).ToArray());
    }

    public async Task<CharacterRevisionSummary[]?> ListRevisions(Guid id, Guid actor, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var allowed = EditableIds(actor).Where(x => x == id);
        if (!await allowed.AnyAsync(ct)) return null;
        return await db.CharacterRevisions.AsNoTracking().Where(r => r.CharacterId == id && allowed.Contains(r.CharacterId))
            .OrderByDescending(r => r.Version).Take(limit)
            .Select(r => new CharacterRevisionSummary(r.Id, r.Version, r.CreatedAt, r.EditedBy.DisplayName)).ToArrayAsync(ct);
    }

    public async Task<CharacterRevisionState?> LoadRevision(Guid id, Guid revisionId, Guid actor, CancellationToken ct)
    {
        var allowed = EditableIds(actor);
        var revision = await db.CharacterRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == revisionId && r.CharacterId == id && allowed.Contains(r.CharacterId), ct);
        if (revision is null) return null;
        var saved = JsonSerializer.Deserialize<CharacterSnapshot>(revision.SnapshotJson)
            ?? throw new InvalidDataException("Invalid persisted character revision.");
        return new(revision.Id, id, new(saved.Name, saved.PortraitUrl, saved.BackgroundUrl, saved.Health, saved.MaxHealth,
            saved.Sanity, saved.MaxSanity, saved.Mana, saved.MaxMana, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(saved.FieldsJson) ?? [], revision.Version));
    }

    public async Task<CharacterWriteResponse> Duplicate(CharacterSheetSnapshot before, CharacterSheetState state, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try
        {
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, actor, before.SystemId, ct);
            var fresh = await Load(before.Id, actor, ct);
            if (fresh is null || fresh.Role == "spectator") return new(Failure: CharacterWriteFailure.NotFound);
            if (!CharacterAccessPolicy.CanEdit(fresh.OwnerId, actor, fresh.Role))
                return new(Failure: CharacterWriteFailure.Forbidden, Message: "Você não pode duplicar este personagem.");
            if (fresh.State.Version != before.State.Version || fresh.CampaignId != before.CampaignId || fresh.OwnerId != before.OwnerId ||
                fresh.Role != before.Role || !CharacterSheetPersistence.SameSystem(before.SystemId, before.System, fresh.SystemId, fresh.System))
                return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha ou o sistema mudou. Atualize antes de duplicar.");
            // All source collections are read from this transaction's snapshot, never from the request body.
            var inventory = await db.CharacterInventoryItems.AsNoTracking().Where(i => i.CharacterId == before.Id).ToListAsync(ct);
            var abilities = await db.CharacterAbilities.AsNoTracking().Where(a => a.CharacterId == before.Id).ToListAsync(ct);
            var copy = new Character { CampaignId = fresh.CampaignId, OwnerId = actor, Name = state.Name, SystemVersionId = fresh.SystemVersionId };
            CharacterSheetPersistence.Apply(copy, state);
            db.Characters.Add(copy);
            foreach (var item in inventory) db.CharacterInventoryItems.Add(new() { CharacterId = copy.Id, Name = item.Name,
                Category = item.Category, Quantity = item.Quantity, Weight = item.Weight, Notes = item.Notes });
            foreach (var ability in abilities) db.CharacterAbilities.Add(new() { CharacterId = copy.Id, Name = ability.Name,
                Description = ability.Description, Cost = ability.Cost, Effect = ability.Effect, Duration = ability.Duration,
                Limitations = ability.Limitations, Recharge = ability.Recharge, RollExpression = ability.RollExpression });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(CharacterSheetPersistence.Snapshot(copy, fresh.Role, fresh.System, fresh.SystemId));
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error))
        {
            db.ChangeTracker.Clear();
            return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha mudou. Atualize antes de duplicar.");
        }
    }

    private IQueryable<Guid> EditableIds(Guid actor) => db.Characters.Where(c => c.Campaign.Memberships.Any(m => m.UserId == actor &&
        m.Role != "spectator" && (c.OwnerId == actor || m.Role == "owner" || m.Role == "gm" || m.Role == "co-gm"))).Select(c => c.Id);
}
