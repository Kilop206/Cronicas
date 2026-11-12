using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chronicles.Domain;

public static class CharacterSheetValidation
{
    public static Dictionary<string, string[]>? Validate(CharacterInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Name?.Trim().Length is not (>= 2 and <= 120)) errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
        if (!ValidUrl(input.PortraitUrl) || !ValidUrl(input.BackgroundUrl)) errors["image"] = ["Use URLs HTTPS de imagens com até 2048 caracteres."];
        if (!ValidResource(input.Health, input.MaxHealth) || !ValidResource(input.Sanity, input.MaxSanity) || !ValidResource(input.Mana, input.MaxMana)) errors["resources"] = ["Cada recurso deve estar entre zero e seu máximo (até 9999)."];
        return errors.Count == 0 ? null : errors;
    }
    private static bool ValidUrl(string? value) => string.IsNullOrWhiteSpace(value) || value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
    private static bool ValidResource(int value, int max) => max is >= 0 and <= 9999 && value >= 0 && value <= max;
    public static Dictionary<string, string[]>? ValidateFields(SystemDefinition? definition,
        Dictionary<string, JsonElement>? values, Dictionary<string, JsonElement>? previousValues = null,
        bool allowGm = false, Dictionary<string, JsonElement>? authorizationValues = null,
        bool ruleExecution = false, bool newCharacter = false, IRpgRulesPlugin? ruleValidator = null)
    {
        if (values is null) return null;
        if (values.Count > 100 || JsonSerializer.Serialize(values).Length > 20000) return new() { ["fields"] = ["Campos excedem o limite."] };
        SystemField[] fields = definition?.Fields ?? [];
        var byKey = fields.ToDictionary(x => x.Key);
        var previous = previousValues ?? new Dictionary<string, JsonElement>();
        var authorizedPrevious = authorizationValues ?? previous;
        var accessError = SheetFieldAccess.RestrictedChange(fields, authorizedPrevious, values, allowGm);
        if (accessError is not null) return new() { ["fields"] = [accessError] };
        foreach (var (key, value) in values)
        {
            if (fields.Length == 0)
            {
                // A model may retire its last declared field. Keep archived structured values unchanged,
                // while newly supplied free fields still obey the primitive-only contract below.
                if (previous.TryGetValue(key, out var archived) && JsonElement.DeepEquals(value, archived)) continue;
                if (!Regex.IsMatch(key, "^[a-z][a-z0-9_]{1,39}$", RegexOptions.CultureInvariant)
                    || value.ValueKind == JsonValueKind.String && value.GetString()?.Length > 10000
                    || value.ValueKind == JsonValueKind.Number && !value.TryGetInt32(out _)
                    || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                    return new() { ["fields"] = ["Campo livre inválido."] };
                continue;
            }
            if (!byKey.TryGetValue(key, out var field))
            {
                if (previous.TryGetValue(key, out var oldValue) && JsonElement.DeepEquals(value, oldValue)) continue;
                return new() { ["fields"] = [$"Campo desconhecido: {key}."] };
            }
            if (!string.IsNullOrWhiteSpace(field.Formula)) continue;
            var valid = SheetCollectionValues.ValidField(field, value);
            if (!valid) return new() { ["fields"] = [$"Valor inválido para {field.Label}."] };
        }
        var validator = ruleValidator ?? (definition is null ? null : RpgPluginRegistry.Default.RulesFor(definition));
        if (definition is not null && validator?.ValidateStateChange(definition, authorizedPrevious, values, allowGm, ruleExecution, newCharacter) is { } stateError)
            return new() { ["fields"] = [stateError] };
        var calculationError = SheetFormula.Apply(fields, values);
        if (calculationError is not null) return new() { ["fields"] = [calculationError] };
        if (values.Count > 100 || JsonSerializer.Serialize(values).Length > 20000) return new() { ["fields"] = ["Campos excedem o limite."] };
        return null;
    }
}
