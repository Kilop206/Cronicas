using System.Text.Json;

namespace Chronicles.Domain;

public sealed record MigrationFieldChange(string Key, string Kind, SystemField? Before, SystemField? After,
    JsonElement? PreviousValue, JsonElement? NextValue);
public sealed record SystemMigrationPlan(Dictionary<string, JsonElement> Fields, MigrationFieldChange[] Changes,
    string[] PreservedKeys, Dictionary<string, string[]> Errors);

// A migration copies renamed values and retains retired keys. It never guesses an edition conversion.
public static class CharacterSystemMigration
{
    public static SystemMigrationPlan Plan(SystemDefinition source, SystemDefinition target,
        Dictionary<string, JsonElement>? existing, Dictionary<string, string>? mappings,
        Dictionary<string, JsonElement>? adjustments, RpgPluginRegistry plugins)
    {
        var original = existing ?? [];
        var values = original.ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal);
        var errors = new Dictionary<string, string[]>();
        void Error(string key, string message) => errors[key] = [message];
        var fields = target.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        if (source.TemplateId != target.TemplateId || source.RulesetVersion != target.RulesetVersion)
            Error("edition", "A troca de sistema ou edição exige um conversor específico, ainda indisponível.");
        if (plugins.Plugins.Any(p => p.Manifest.Id == target.TemplateId) && plugins.Resolve(target) is null)
            Error("plugin", "Esta versão do schema não é suportada pelo plugin instalado.");
        if (plugins.RulesFor(source) is not null && plugins.RulesFor(target) is null)
            Error("plugin", "O destino não possui os campos e o executor de regras necessários.");
        if ((mappings?.Count ?? 0) > 100 || (adjustments?.Count ?? 0) > 100)
            Error("limits", "A migração aceita até 100 mapeamentos e ajustes.");
        else
        {
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (from, to) in mappings ?? [])
            {
                if (string.IsNullOrWhiteSpace(to) || !original.TryGetValue(from, out var value) || !fields.TryGetValue(to, out var field) ||
                    from == to || !string.IsNullOrWhiteSpace(field.Formula))
                    Error("mapping:" + from, "Mapeie um valor existente para um campo editável do destino.");
                else if (original.ContainsKey(to) || !destinations.Add(to) || adjustments?.ContainsKey(to) == true)
                    Error("mapping:" + from, "Destino ocupado ou repetido. Nenhum valor será sobrescrito pelo mapeamento.");
                else values[to] = value.Clone();
            }
            foreach (var (key, value) in adjustments ?? [])
            {
                if (!fields.TryGetValue(key, out var field) || !string.IsNullOrWhiteSpace(field.Formula))
                    Error("value:" + key, "Ajuste apenas campos editáveis do modelo de destino.");
                else values[key] = value.Clone();
            }
            foreach (var field in target.Fields)
                if (!values.ContainsKey(field.Key) && field.DefaultValue is { } initial && string.IsNullOrWhiteSpace(field.Formula))
                    values[field.Key] = initial.Clone();
            foreach (var field in target.Fields)
                if (values.TryGetValue(field.Key, out var value) && string.IsNullOrWhiteSpace(field.Formula) && !SheetCollectionValues.ValidField(field, value))
                    Error("value:" + field.Key, $"Ajuste {field.Label}: tipo ou limites incompatíveis com o destino.");
        }
        if (errors.Count == 0 && CharacterSheetValidation.ValidateFields(target, values, original, allowGm: true,
                ruleValidator: plugins.RulesFor(target)) is { } validation)
            foreach (var (key, messages) in validation) errors[key] = messages;
        var oldFields = source.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var keys = oldFields.Keys.Concat(fields.Keys).Concat(original.Keys).Concat(values.Keys).Distinct(StringComparer.Ordinal);
        var changes = keys.Select(key =>
        {
            var oldField = oldFields.GetValueOrDefault(key); var nextField = fields.GetValueOrDefault(key);
            var had = original.TryGetValue(key, out var oldValue); var has = values.TryGetValue(key, out var nextValue);
            var kind = oldField is null && nextField is not null ? "added" : oldField is not null && nextField is null ? "retired" :
                JsonSerializer.Serialize(oldField) != JsonSerializer.Serialize(nextField) ? "definition" :
                had != has || had && !JsonElement.DeepEquals(oldValue, nextValue) ? "value" : "unchanged";
            return new MigrationFieldChange(key, kind, oldField, nextField, had ? oldValue.Clone() : null, has ? nextValue.Clone() : null);
        }).Where(c => c.Kind != "unchanged").ToArray();
        return new(values, changes, values.Keys.Where(key => !fields.ContainsKey(key)).ToArray(), errors);
    }
}
