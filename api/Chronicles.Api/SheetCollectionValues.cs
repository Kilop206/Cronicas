using System.Text.Json;

namespace Chronicles.Api;

public static class SheetCollectionValues
{
    public static bool ValidField(SystemField field, JsonElement value)
    {
        if (!string.IsNullOrWhiteSpace(field.Formula)) return false;
        return field.Type switch
        {
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                && (field.Min is null || number >= field.Min) && (field.Max is null || number <= field.Max),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "select" => value.ValueKind == JsonValueKind.String && field.Options?.Contains(value.GetString()) == true,
            "text" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 300,
            "longtext" => value.ValueKind == JsonValueKind.String && value.GetString()?.Length <= 10000,
            "list" => ValidList(value),
            "table" => ValidTable(field, value),
            _ => false
        };
    }
    public static bool ValidList(JsonElement value) => value.ValueKind == JsonValueKind.Array
        && value.GetArrayLength() <= 30
        && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String && item.GetString()?.Length <= 300);

    public static bool ValidTable(SystemField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 30 || field.Columns is not { Length: > 0 } columns) return false;
        var byKey = columns.ToDictionary(column => column.Key, StringComparer.Ordinal);
        foreach (var row in value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) return false;
            foreach (var cell in row.EnumerateObject())
            {
                if (!byKey.TryGetValue(cell.Name, out var column)) return false;
                var valid = column.Type switch
                {
                    "text" => cell.Value.ValueKind == JsonValueKind.String && cell.Value.GetString()?.Length <= 300,
                    "number" => cell.Value.ValueKind == JsonValueKind.Number && cell.Value.TryGetInt32(out _),
                    "boolean" => cell.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                    _ => false
                };
                if (!valid) return false;
            }
        }
        return true;
    }
}
