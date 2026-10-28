using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api;

public static class SystemEndpoints
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{1,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/systems", async Task<IResult> (HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var systems = await db.RpgSystems.AsNoTracking().Where(x => x.OwnerId == userId).OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(systems.Select(View));
        });

        api.MapGet("/systems/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (system is null || system.OwnerId != userId && !await db.CampaignMemberships.AnyAsync(x => x.UserId == userId && x.Campaign.SystemId == id)) return Results.NotFound();
            return Results.Ok(View(system));
        });

        api.MapPost("/systems", async Task<IResult> (SystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var system = new RpgSystem { OwnerId = userId, Name = input.Name.Trim(), Description = input.Description.Trim(), SchemaJson = JsonSerializer.Serialize(Definition(input)) };
            db.RpgSystems.Add(system);
            await db.SaveChangesAsync();
            return Results.Created($"/api/systems/{system.Id}", View(system));
        });

        api.MapPut("/systems/{id:guid}", async Task<IResult> (Guid id, SystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (system is null) return Results.NotFound();
            var previous = SystemDefinition.Read(system.SchemaJson);
            if (input.Revision is { } expected && expected != previous.Revision)
                return Results.Conflict(new { message = "O modelo mudou. Recarregue antes de salvar." });
            var errors = Validate(input);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var history = (previous.History ?? []).Append(Snapshot(system, previous)).TakeLast(10).ToArray();
            var next = Definition(input) with { Revision = previous.Revision + 1, History = history,
                NpcTemplates = input.NpcTemplates ?? previous.NpcTemplates };
            var renames = input.FieldRenames ?? new Dictionary<string, string>();
            if (renames.Count > 100 || renames.Values.Distinct(StringComparer.Ordinal).Count() != renames.Count
                || renames.Any(pair => pair.Key == pair.Value
                    || previous.Fields.FirstOrDefault(field => field.Key == pair.Key) is not { } oldField
                    || input.Fields.FirstOrDefault(field => field.Key == pair.Value) is not { } newField
                    || oldField.Type != newField.Type))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["fieldRenames"] = ["Migração de campos inválida."] });
            var fieldsChanged = JsonSerializer.Serialize(previous.Fields) != JsonSerializer.Serialize(input.Fields);
            if (renames.Count > 0 || fieldsChanged)
            {
                var characters = await db.Characters.Where(character => character.Campaign.SystemId == id).ToListAsync();
                foreach (var character in characters)
                {
                    var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.FieldsJson) ?? [];
                    var migrated = SheetSchemaMigration.Apply(values, renames, input.Fields);
                    if (migrated.Error is not null)
                        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fieldRenames"] = [migrated.Error] });
                    if (!migrated.Changed) continue;
                    CharacterHistory.Capture(db, character, userId);
                    character.FieldsJson = JsonSerializer.Serialize(migrated.Values);
                    character.UpdatedAt = DateTime.UtcNow;
                }
            }
            system.Name = input.Name.Trim(); system.Description = input.Description.Trim(); system.SchemaJson = JsonSerializer.Serialize(next); system.UpdatedAt = DateTime.UtcNow;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O modelo mudou. Recarregue antes de salvar." }); }
            return Results.Ok(View(system));
        });

        api.MapPost("/systems/{id:guid}/revisions/{revision:int}/restore", async Task<IResult> (Guid id, int revision, RestoreSystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (system is null) return Results.NotFound();
            var current = SystemDefinition.Read(system.SchemaJson);
            if (input.ExpectedRevision != current.Revision) return Results.Conflict(new { message = "O modelo mudou. Recarregue antes de restaurar." });
            var target = current.History?.LastOrDefault(item => item.Revision == revision);
            if (target is null) return Results.NotFound();
            var characters = await db.Characters.Where(character => character.Campaign.SystemId == id).ToListAsync();
            foreach (var character in characters)
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(character.FieldsJson) ?? [];
                var migrated = SheetSchemaMigration.Apply(values, new Dictionary<string, string>(), target.Fields);
                if (migrated.Error is not null)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["revision"] = [migrated.Error] });
                if (!migrated.Changed) continue;
                CharacterHistory.Capture(db, character, userId);
                character.FieldsJson = JsonSerializer.Serialize(migrated.Values);
                character.UpdatedAt = DateTime.UtcNow;
            }
            var history = (current.History ?? []).Append(Snapshot(system, current)).TakeLast(10).ToArray();
            system.Name = target.Name; system.Description = target.Description;
            system.SchemaJson = JsonSerializer.Serialize(new SystemDefinition(target.Fields, target.Layout, target.TemplateId,
                target.TemplateVersion, target.RulesetVersion, target.RendererMode, current.Revision + 1, history, target.NpcTemplates));
            system.UpdatedAt = DateTime.UtcNow;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Results.Conflict(new { message = "O modelo mudou. Recarregue antes de restaurar." }); }
            return Results.Ok(View(system));
        });

        api.MapDelete("/systems/{id:guid}", async Task<IResult> (Guid id, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var system = await db.RpgSystems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
            if (system is null) return Results.NotFound();
            if (await db.Campaigns.AnyAsync(x => x.SystemId == id)) return Results.Conflict(new { message = "Remova este sistema das campanhas antes de excluí-lo." });
            db.RpgSystems.Remove(system); await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapPatch("/campaigns/{campaignId:guid}/system", async Task<IResult> (Guid campaignId, SetSystemInput input, HttpContext http, AppDbContext db) =>
        {
            if (UserId(http) is not { } userId) return Results.Unauthorized();
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == userId);
            if (campaign is null) return Results.NotFound();
            var allowed = input.SystemId is null || await db.RpgSystems.AnyAsync(x => x.Id == input.SystemId && x.OwnerId == userId);
            if (!allowed) return Results.ValidationProblem(new Dictionary<string, string[]> { ["systemId"] = ["Sistema indisponível."] });
            campaign.SystemId = input.SystemId; campaign.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static SystemDefinition Definition(SystemInput input) => new(input.Fields, input.Layout, input.TemplateId,
        input.TemplateVersion, input.RulesetVersion, input.RendererMode, NpcTemplates: input.NpcTemplates);
    private static SystemRevision Snapshot(RpgSystem system, SystemDefinition definition) => new(definition.Revision,
        system.UpdatedAt, system.Name, system.Description, definition.Fields, definition.Layout,
        definition.TemplateId, definition.TemplateVersion, definition.RulesetVersion, definition.RendererMode, definition.NpcTemplates);
    private static object View(RpgSystem system)
    {
        var definition = SystemDefinition.Read(system.SchemaJson);
        return new { system.Id, system.OwnerId, system.Name, system.Description, fields = definition.Fields, layout = definition.Layout,
            definition.TemplateId, definition.TemplateVersion, definition.RulesetVersion, definition.RendererMode,
            definition.Revision, npcTemplates = definition.NpcTemplates ?? [], revisions = (definition.History ?? []).Select(item => new { item.Revision, item.SavedAt, item.Name }),
            system.CreatedAt, system.UpdatedAt };
    }
    private static Dictionary<string, string[]> Validate(SystemInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.TemplateId is not null && !KeyPattern.IsMatch(input.TemplateId)
            || input.TemplateVersion is < 1 or > 1000
            || input.RulesetVersion is not null && input.RulesetVersion.Length > 40)
            errors["template"] = ["Identificador ou versao do modelo invalidos."];
        if (input.RendererMode is not (null or "specialized" or "layout")) errors["rendererMode"] = ["Modo de ficha invalido."];
        if (input.NpcTemplates is { } templates && (templates.Length > 12 || templates.Any(template => template is null || template.Id is null || !KeyPattern.IsMatch(template.Id)
            || template.Name?.Trim().Length is not (>= 2 and <= 80) || template.Appearance?.Length > 2000 || template.Personality?.Length > 2000
            || template.Motivation?.Length > 2000 || template.Relationships?.Length > 2000 || template.Location?.Length > 160
            || template.Status is not (null or "active" or "missing" or "deceased" or "unknown")
            || template.Visibility is not (null or "gm" or "campaign") || template.MaxHealth is < 1 or > 9999
            || template.Health is < 0 or > 9999 || template.Health > template.MaxHealth
            || (template.Health is null) != (template.MaxHealth is null))
            || templates.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() != templates.Length))
            errors["npcTemplates"] = ["Modelo de NPC inválido ou repetido."];
        if (input.Name?.Trim().Length is not (>= 3 and <= 120) || input.Description is null || input.Description.Length > 2000) errors["system"] = ["Nome ou descrição inválidos."];
        if (input.Fields is null || input.Fields.Length > 100) { errors["fields"] = ["Defina até 100 campos."]; return errors; }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in input.Fields)
        {
            if (field is null || field.Key is null || !KeyPattern.IsMatch(field.Key) || !keys.Add(field.Key) || field.Label?.Trim().Length is not (>= 2 and <= 80)
                || field.Section?.Trim().Length is not (>= 2 and <= 60) || field.Type is not ("text" or "longtext" or "number" or "boolean" or "select" or "list" or "table")
                || field.Span is < 1 or > 12
                || field.Type == "number" && (field.Min is < -999999 or > 999999 || field.Max is < -999999 or > 999999 || field.Min > field.Max)
                || field.Type == "select" && (field.Options is null || field.Options.Length is < 1 or > 30 || field.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80))
                || field.Type == "table" && (field.Columns is null || field.Columns.Length is < 1 or > 8
                    || field.Columns.Any(column => column is null || column.Key is null || !KeyPattern.IsMatch(column.Key)
                        || column.Label?.Trim().Length is not (>= 2 and <= 80) || column.Type is not ("text" or "number" or "boolean" or "dice"))
                    || field.Columns.Select(column => column.Key).Distinct(StringComparer.Ordinal).Count() != field.Columns.Length)
                || field.Type != "table" && field.Columns is { Length: > 0 }
                || field.EditRole is not (null or "both" or "gm")
                || !ValidDefault(field))
            { errors["fields"] = ["Campo inválido, chave repetida ou limites fora do intervalo."]; break; }
        }
        if (errors.ContainsKey("fields")) return errors;
        var formulaError = SheetFormula.Validate(input.Fields);
        if (formulaError is not null) errors["fields"] = [formulaError];
        if (errors.ContainsKey("fields")) return errors;
        var layoutError = SystemDefinition.ValidateLayout(input.Layout, input.Fields);
        if (layoutError is not null) errors["layout"] = [layoutError];
        return errors;
    }
    private static bool ValidDefault(SystemField field)
    {
        if (field.DefaultValue is not { } value) return true;
        return field.Type switch
        {
            "number" => string.IsNullOrWhiteSpace(field.Formula) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                && (field.Min is null || number >= field.Min) && (field.Max is null || number <= field.Max),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "select" => value.ValueKind == JsonValueKind.String && field.Options?.Contains(value.GetString()) == true,
            "longtext" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 10000,
            "text" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 300,
            "list" => SheetCollectionValues.ValidList(value),
            "table" => SheetCollectionValues.ValidTable(field, value),
            _ => false
        };
    }
    public sealed record SystemInput(string Name, string Description, SystemField[] Fields, SheetLayout? Layout = null,
        string? TemplateId = null, int TemplateVersion = 1, string? RulesetVersion = null,
        string? RendererMode = null, int? Revision = null, Dictionary<string, string>? FieldRenames = null,
        NpcTemplate[]? NpcTemplates = null);
    public sealed record RestoreSystemInput(int ExpectedRevision);
    public sealed record SetSystemInput(Guid? SystemId);
}
