using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record SystemMigrationTarget(Guid? VersionId, string Name, int Revision, string? TemplateId,
    int TemplateVersion, string? RulesetVersion, string ContentHash, SystemField[] Fields, bool Current);
public sealed record SystemMigrationDestination(SystemMigrationTarget Summary, FrozenSystemTemplate Template);
public sealed record SystemMigrationInput(int ExpectedVersion, Guid SourceVersionId, Guid? TargetVersionId,
    int TargetRevision, string TargetHash, Dictionary<string, string>? Mappings = null, Dictionary<string, JsonElement>? Values = null);
public sealed record SystemMigrationReceipt(Guid SourceVersionId, Guid TargetVersionId, string PreviewHash,
    Dictionary<string, string> Mappings, string[] AdjustedKeys, DateTime AppliedAt);
public sealed record SystemMigrationAudit(int CharacterRevision, Guid EditedBy, SystemMigrationReceipt Receipt);
public sealed record SystemMigrationPreview(SystemMigrationTarget Target, SystemMigrationPlan Plan, string? PreviewHash)
{
    public bool CanApply => PreviewHash is not null && Plan.Errors.Count == 0;
}
public sealed record SystemMigrationOptions(SystemMigrationTarget[] Targets, SystemMigrationAudit[] History);
public sealed record SystemMigrationTargetsResponse(SystemMigrationOptions? Options = null, CharacterWriteFailure? Failure = null, string? Message = null);
public sealed record SystemMigrationPreviewResponse(SystemMigrationPreview? Preview = null, CharacterWriteFailure? Failure = null, string? Message = null);

public interface ICharacterSystemMigrationStore : ICharacterSheetReader
{
    Task<SystemMigrationTarget[]> Targets(CharacterSheetSnapshot before, Guid actor, CancellationToken ct);
    Task<SystemMigrationAudit[]> History(CharacterSheetSnapshot before, CancellationToken ct);
    Task<SystemMigrationDestination?> Destination(CharacterSheetSnapshot before, Guid actor, Guid? version, CancellationToken ct);
    Task<CharacterWriteResponse> Commit(CharacterSheetSnapshot before, Guid actor, SystemMigrationInput input, string previewHash, CancellationToken ct);
}

public sealed class MigrateCharacterSystem(ICharacterSystemMigrationStore store, RpgPluginRegistry plugins)
{
    public async Task<SystemMigrationTargetsResponse> Targets(Guid character, Guid actor, CancellationToken ct = default)
    {
        var before = await store.Load(character, actor, ct);
        var failure = Access(before);
        return failure is not null ? new(Failure: failure, Message: AccessMessage) :
            new(new(await store.Targets(before!, actor, ct), await store.History(before!, ct)));
    }

    public async Task<SystemMigrationPreviewResponse> Preview(Guid character, Guid actor, SystemMigrationInput input, CancellationToken ct = default)
    {
        var before = await store.Load(character, actor, ct);
        var failure = Access(before);
        if (failure is not null) return new(Failure: failure, Message: AccessMessage);
        var target = await store.Destination(before!, actor, input.TargetVersionId, ct);
        return Review(before!, actor, target, input, plugins);
    }

    public async Task<CharacterWriteResponse> Apply(Guid character, Guid actor, SystemMigrationInput input, string previewHash, CancellationToken ct = default)
    {
        var before = await store.Load(character, actor, ct);
        var failure = Access(before);
        if (failure is not null) return new(Failure: failure, Message: AccessMessage);
        var target = await store.Destination(before!, actor, input.TargetVersionId, ct);
        var result = Review(before!, actor, target, input, plugins);
        if (result.Failure is not null) return new(Failure: result.Failure, Message: result.Message);
        if (!result.Preview!.CanApply) return new(Failure: CharacterWriteFailure.Invalid, Errors: result.Preview.Plan.Errors);
        if (!ValidHash(previewHash) || result.Preview.PreviewHash != previewHash) return Conflict();
        return await store.Commit(before!, actor, input, previewHash, ct);
    }

    public static SystemMigrationPreviewResponse Review(CharacterSheetSnapshot before, Guid actor,
        SystemMigrationDestination? target, SystemMigrationInput input, RpgPluginRegistry plugins)
    {
        if (target is null) return new(Failure: CharacterWriteFailure.NotFound);
        if (input.ExpectedVersion != before.State.Version || input.SourceVersionId != before.SystemVersionId ||
            input.TargetRevision != target.Summary.Revision || input.TargetHash != target.Summary.ContentHash)
            return new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha ou o destino mudou. Gere uma nova prévia.");
        var plan = CharacterSystemMigration.Plan(before.System!, target.Template.Definition, before.State.Fields, input.Mappings, input.Values, plugins);
        if (before.SystemVersionId == target.Summary.VersionId)
            plan.Errors["target"] = ["Escolha uma versão diferente da versão fixada."];
        var hash = plan.Errors.Count == 0 ? Digest(new { Actor = actor, before.Id, before.CampaignId, before.SystemVersionId,
            before.State, Target = target.Summary.ContentHash, Input = input, plan.Fields }) : null;
        return new(new(target.Summary, plan, hash));
    }

    public static CharacterWriteResponse Conflict() => new(Failure: CharacterWriteFailure.Conflict, Message: "A ficha, o destino ou o plano mudou. Gere e revise uma nova prévia.");
    private const string AccessMessage = "Somente mestres migram fichas com uma versão fixada.";
    private static CharacterWriteFailure? Access(CharacterSheetSnapshot? before) => before is null ? CharacterWriteFailure.NotFound :
        !CharacterAccessPolicy.IsMaster(before.Role) ? CharacterWriteFailure.Forbidden :
        before.SystemVersionId is null || before.System is null ? CharacterWriteFailure.Invalid : null;
    private static bool ValidHash(string? hash) => hash?.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    // Canonical objects make input key ordering irrelevant. This is a review/concurrency digest, not an authorization token.
    private static string Digest<T>(T value)
    {
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        void Write(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            { writer.WriteStartObject(); foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(p.Value); } writer.WriteEndObject(); }
            else if (element.ValueKind == JsonValueKind.Array)
            { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(item); writer.WriteEndArray(); }
            else element.WriteTo(writer);
        }
        Write(JsonSerializer.SerializeToElement(value)); writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
