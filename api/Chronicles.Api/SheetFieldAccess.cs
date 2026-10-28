using System.Text.Json;

namespace Chronicles.Api;

public static class SheetFieldAccess
{
    public static string? RestrictedChange(SystemField[] fields, Dictionary<string, JsonElement> before,
        Dictionary<string, JsonElement> after, bool isMaster)
    {
        if (isMaster) return null;
        foreach (var field in fields.Where(item => item.EditRole == "gm"))
        {
            var hadValue = before.TryGetValue(field.Key, out var oldValue);
            var hasValue = after.TryGetValue(field.Key, out var nextValue);
            if (hadValue != hasValue || hadValue && !JsonElement.DeepEquals(oldValue, nextValue))
                return $"O campo {field.Label} pode ser alterado somente pelo mestre.";
        }
        return null;
    }
}
