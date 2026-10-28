using System.Text.Json;

namespace Chronicles.Api;

public static class SheetSchemaMigration
{
    public static (Dictionary<string, JsonElement> Values, bool Changed, string? Error) Apply(
        Dictionary<string, JsonElement> existing, IReadOnlyDictionary<string, string> renames, SystemField[] fields)
    {
        var values = new Dictionary<string, JsonElement>(existing, StringComparer.Ordinal);
        var byKey = fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        foreach (var (from, to) in renames)
        {
            if (!values.TryGetValue(from, out var value) || values.ContainsKey(to)) continue;
            if (!byKey.TryGetValue(to, out var destination) || !SheetCollectionValues.ValidField(destination, value))
                return (existing, false, $"O valor de {from} não cabe no novo campo {to}.");
            values[to] = value;
            values.Remove(from);
        }
        foreach (var (key, value) in values)
            if (byKey.TryGetValue(key, out var field) && string.IsNullOrWhiteSpace(field.Formula)
                && !SheetCollectionValues.ValidField(field, value))
                return (existing, false, $"O valor existente de {key} é incompatível com o novo modelo.");
        var calculationError = SheetFormula.Apply(fields, values);
        if (calculationError is not null) return (existing, false, calculationError);
        if (values.Count > 100 || JsonSerializer.Serialize(values).Length > 20000)
            return (existing, false, "Os campos migrados excedem o limite da ficha.");
        return (values, !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(existing), JsonSerializer.SerializeToElement(values)), null);
    }
}
