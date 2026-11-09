using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterSystemStore(AppDbContext db) : ICharacterSystemStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid character, Guid actor, CancellationToken ct) => CharacterSheetPersistence.Load(db, character, actor, ct);

    public async Task<CharacterSystemRead> ReadSystem(Guid character, Guid actor, CancellationToken ct)
    {
        var row = await db.Characters.AsNoTracking().Where(c => c.Id == character && c.Campaign.Memberships.Any(m => m.UserId == actor))
            .Select(c => new { Current = c.Campaign.System, Frozen = c.SystemVersion,
                FrozenOwner = c.SystemVersion == null ? null : c.SystemVersion.System.OwnerId,
                Role = c.Campaign.Memberships.Where(m => m.UserId == actor).Select(m => m.Role).First() }).SingleOrDefaultAsync(ct);
        if (row is null) return new(false, null);
        var version = row.Frozen; var system = row.Current;
        if (version is null && system is null) return new(true, null);
        var definition = SystemDefinition.Read(version?.DefinitionJson ?? system!.SchemaJson);
        var owner = version is null ? system!.OwnerId : row.FrozenOwner;
        return new(true, new(version?.RpgSystemId ?? system!.Id, owner, version?.Name ?? system!.Name,
            version?.Description ?? system!.Description, definition.Fields, definition.Layout, definition.TemplateId,
            definition.TemplateVersion, definition.RulesetVersion, definition.RendererMode, definition.Revision,
            (definition.History ?? []).Select(r => new CharacterSystemRevisionSummary(r.Revision, r.SavedAt, r.Name)).ToArray(),
            (definition.NpcTemplates ?? []).Where(t => owner == actor || CharacterAccessPolicy.IsMaster(row.Role) || t.Sheet is null).ToArray(),
            definition.CthulhuOccupations, version?.CreatedAt ?? system!.CreatedAt, version?.CreatedAt ?? system!.UpdatedAt, version?.Id));
    }

    public async Task<CharacterWriteResponse> Pin(CharacterSheetSnapshot before, Guid actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            // Serialize archive creation per source system; templates and authorization stay stable until commit.
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, actor, before.SystemId, ct, exclusiveSystem: true);
            var fresh = await Load(before.Id, actor, ct);
            if (fresh is null) return new(Failure: CharacterWriteFailure.NotFound);
            if (!CharacterAccessPolicy.IsMaster(fresh.Role)) return new(Failure: CharacterWriteFailure.Forbidden);
            if (fresh.CampaignId != before.CampaignId || fresh.State.Version != before.State.Version || fresh.SystemVersionId != before.SystemVersionId || fresh.Role != before.Role ||
                !CharacterSheetPersistence.SameSystem(before.SystemId, before.System, fresh.SystemId, fresh.System)) return Conflict();
            if (fresh.SystemVersionId is not null) return new(fresh);
            var source = await db.RpgSystems.AsNoTracking().SingleAsync(s => s.Id == fresh.SystemId, ct);
            var frozen = FrozenSystemTemplate.Capture(source.Name, source.Description, fresh.System!);
            var hash = frozen.Fingerprint();
            var version = await db.RpgSystemVersions.SingleOrDefaultAsync(v => v.RpgSystemId == source.Id && v.ContentHash == hash, ct);
            if (version is null)
            {
                version = new() { RpgSystemId = source.Id, Name = frozen.Name, Description = frozen.Description,
                    DefinitionJson = JsonSerializer.Serialize(frozen.Definition), ContentHash = hash, SourceRevision = frozen.Definition.Revision };
                db.RpgSystemVersions.Add(version);
            }
            var character = await db.Characters.SingleAsync(c => c.Id == before.Id, ct);
            CharacterHistory.Capture(db, character, actor);
            character.SystemVersion = version; character.SystemVersionId = version.Id; character.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(CharacterSheetPersistence.Snapshot(character, fresh.Role, frozen.Definition, source.Id));
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error)) { db.ChangeTracker.Clear(); return Conflict(); }
    }

    private static CharacterWriteResponse Conflict() => new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha ou o sistema mudou. Recarregue antes de fixar a versão.");
}
