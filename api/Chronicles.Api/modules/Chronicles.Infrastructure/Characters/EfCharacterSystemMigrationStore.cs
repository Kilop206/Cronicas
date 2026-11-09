using System.Text.Json;
using Chronicles.Api;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Chronicles.Application.CharacterSheets;
using Chronicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Infrastructure.Characters;

public sealed class EfCharacterSystemMigrationStore(AppDbContext db, RpgPluginRegistry plugins) : ICharacterSystemMigrationStore
{
    public Task<CharacterSheetSnapshot?> Load(Guid character, Guid actor, CancellationToken ct) => CharacterSheetPersistence.Load(db, character, actor, ct);

    private async Task<bool> CanReadSource(CharacterSheetSnapshot before, Guid actor, CancellationToken ct) =>
        await db.RpgSystems.AnyAsync(s => s.Id == before.SystemId && (s.OwnerId == actor ||
            db.Campaigns.Any(c => c.Id == before.CampaignId && c.SystemId == s.Id)), ct);

    private async Task<RpgSystemVersion[]> Archives(CharacterSheetSnapshot before, Guid actor, CancellationToken ct)
    {
        var source = before.SystemId!.Value;
        if (await CanReadSource(before, actor, ct))
            return await db.RpgSystemVersions.AsNoTracking().Where(v => v.RpgSystemId == source).OrderByDescending(v => v.CreatedAt).ToArrayAsync(ct);
        // Detached campaigns retain access to their character's history, never to future private source versions.
        return await db.RpgSystemVersions.FromSqlInterpolated($"""
            SELECT v.* FROM rpg_system_versions v WHERE v."RpgSystemId" = {source} AND
            (v."Id" = {before.SystemVersionId} OR EXISTS (
                SELECT 1 FROM character_revisions r WHERE r."CharacterId" = {before.Id} AND
                (r."SnapshotJson"->>'SystemVersionId' = v."Id"::text OR
                 r."SnapshotJson"->'Migration'->>'TargetVersionId' = v."Id"::text)))
            ORDER BY v."CreatedAt" DESC
            """).AsNoTracking().ToArrayAsync(ct);
    }

    public async Task<SystemMigrationTarget[]> Targets(CharacterSheetSnapshot before, Guid actor, CancellationToken ct)
    {
        var result = (await Archives(before, actor, ct)).Select(v => Summary(v)).ToList();
        if (await Destination(before, actor, null, ct) is { } current) result.Insert(0, current.Summary);
        return result.ToArray();
    }

    public async Task<SystemMigrationAudit[]> History(CharacterSheetSnapshot before, CancellationToken ct)
    {
        var rows = await db.CharacterRevisions.FromSqlInterpolated($"""
            SELECT * FROM character_revisions WHERE "CharacterId" = {before.Id} AND "SnapshotJson"->'Migration' IS NOT NULL
            AND "SnapshotJson"->'Migration' <> 'null'::jsonb
            """).AsNoTracking().OrderByDescending(r => r.Version).Take(50).ToArrayAsync(ct);
        return rows.Select(row => new SystemMigrationAudit(row.Version + 1, row.EditedById,
            JsonSerializer.Deserialize<CharacterSnapshot>(row.SnapshotJson)!.Migration!)).ToArray();
    }

    public async Task<SystemMigrationDestination?> Destination(CharacterSheetSnapshot before, Guid actor, Guid? version, CancellationToken ct)
    {
        if (version is { } id)
        {
            var archive = (await Archives(before, actor, ct)).SingleOrDefault(v => v.Id == id);
            return archive is null ? null : new(Summary(archive), new(archive.Name, archive.Description, SystemDefinition.Read(archive.DefinitionJson)));
        }
        if (!await CanReadSource(before, actor, ct)) return null;
        var source = await db.RpgSystems.AsNoTracking().SingleOrDefaultAsync(s => s.Id == before.SystemId, ct);
        if (source is null) return null;
        var frozen = FrozenSystemTemplate.Capture(source.Name, source.Description, SystemDefinition.Read(source.SchemaJson));
        var existing = await db.RpgSystemVersions.Where(v => v.RpgSystemId == source.Id && v.ContentHash == frozen.Fingerprint()).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(ct);
        return new(new(existing, frozen.Name, frozen.Definition.Revision, frozen.Definition.TemplateId, frozen.Definition.TemplateVersion,
            frozen.Definition.RulesetVersion, frozen.Fingerprint(), frozen.Definition.Fields, true), frozen);
    }

    public async Task<CharacterWriteResponse> Commit(CharacterSheetSnapshot before, Guid actor, SystemMigrationInput input, string previewHash, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            await CharacterSheetPersistence.LockContext(db, before.CampaignId, actor, before.SystemId, ct, exclusiveSystem: true);
            var rows = await db.Characters.FromSqlInterpolated($"SELECT * FROM characters WHERE \"Id\" = {before.Id} FOR UPDATE").ToListAsync(ct);
            var fresh = await Load(before.Id, actor, ct);
            if (fresh is null) return new(Failure: CharacterWriteFailure.NotFound);
            if (!CharacterAccessPolicy.IsMaster(fresh.Role)) return new(Failure: CharacterWriteFailure.Forbidden);
            if (fresh.CampaignId != before.CampaignId || fresh.SystemId != before.SystemId || fresh.SystemVersionId != before.SystemVersionId ||
                fresh.State.Version != before.State.Version || fresh.Role != before.Role) return MigrateCharacterSystem.Conflict();
            var destination = await Destination(fresh, actor, input.TargetVersionId, ct);
            var reviewed = MigrateCharacterSystem.Review(fresh, actor, destination, input, plugins);
            if (reviewed.Failure is { } failure) return new(Failure: failure, Message: reviewed.Message);
            var preview = reviewed.Preview!;
            if (!preview.CanApply) return new(Failure: CharacterWriteFailure.Invalid, Errors: preview.Plan.Errors);
            if (preview.PreviewHash != previewHash) return MigrateCharacterSystem.Conflict();
            var target = destination!;
            var version = await db.RpgSystemVersions.SingleOrDefaultAsync(v => v.RpgSystemId == fresh.SystemId && v.ContentHash == target.Summary.ContentHash, ct);
            if (version is null)
            {
                version = new() { RpgSystemId = fresh.SystemId!.Value, Name = target.Template.Name, Description = target.Template.Description,
                    DefinitionJson = JsonSerializer.Serialize(target.Template.Definition), ContentHash = target.Summary.ContentHash, SourceRevision = target.Summary.Revision };
                db.RpgSystemVersions.Add(version);
            }
            if (version.Id == fresh.SystemVersionId) return new(Failure: CharacterWriteFailure.Invalid, Errors: new() { ["target"] = ["Este conteúdo já está fixado na ficha."] });
            var character = rows.Single();
            var receipt = new SystemMigrationReceipt(fresh.SystemVersionId!.Value, version.Id, previewHash,
                new(input.Mappings ?? []), (input.Values ?? []).Keys.Order(StringComparer.Ordinal).ToArray(), DateTime.UtcNow);
            CharacterHistory.Capture(db, character, actor, receipt);
            CharacterSheetPersistence.Apply(character, fresh.State with { Fields = preview.Plan.Fields });
            character.SystemVersionId = version.Id; character.SystemVersion = version;
            await CharacterEncounterConditions.Sync(db, character, target.Template.Definition);
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(CharacterSheetPersistence.Snapshot(character, fresh.Role, target.Template.Definition, fresh.SystemId));
        }
        catch (Exception error) when (CharacterSheetPersistence.IsConflict(error)) { db.ChangeTracker.Clear(); return MigrateCharacterSystem.Conflict(); }
        catch (ArgumentException error) { db.ChangeTracker.Clear(); return new(Failure: CharacterWriteFailure.Invalid, Errors: new() { ["rules"] = [error.Message] }); }
    }

    private static SystemMigrationTarget Summary(RpgSystemVersion version)
    {
        var definition = SystemDefinition.Read(version.DefinitionJson);
        return new(version.Id, version.Name, version.SourceRevision, definition.TemplateId, definition.TemplateVersion,
            definition.RulesetVersion, version.ContentHash, definition.Fields, false);
    }
}
