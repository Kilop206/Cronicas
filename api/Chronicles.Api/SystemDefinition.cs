using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chronicles.Api;

public sealed record SheetBlock(string Id, string Kind, int Span, string? Title = null, string? Key = null,
    string? Text = null, string? Style = null);
public sealed record SheetPage(string Id, string Title, SheetBlock[] Blocks);
public sealed record SheetLayout(string AccentColor, SheetPage[] Pages);
public sealed record SystemDefinition(SystemField[] Fields, SheetLayout? Layout)
{
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9_]{1,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SystemDefinition Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new(JsonSerializer.Deserialize<SystemField[]>(json) ?? [], null)
            : JsonSerializer.Deserialize<SystemDefinition>(json) ?? new([], null);
    }

    public static string? ValidateLayout(SheetLayout? layout, SystemField[] fields)
    {
        if (layout is null) return null;
        if (layout.AccentColor is null || !ColorPattern.IsMatch(layout.AccentColor) || layout.Pages is null || layout.Pages.Length is < 1 or > 8)
            return "Defina uma cor hexadecimal e de 1 a 8 páginas.";
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        var blockIds = new HashSet<string>(StringComparer.Ordinal);
        var fieldKeys = fields.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var total = 0;
        foreach (var page in layout.Pages)
        {
            if (page is null || page.Id is null || !IdPattern.IsMatch(page.Id) || !pageIds.Add(page.Id)
                || page.Title?.Trim().Length is not (>= 2 and <= 60) || page.Blocks is null || page.Blocks.Length is < 1 or > 40)
                return "Página da ficha inválida ou repetida.";
            total += page.Blocks.Length;
            if (total > 100) return "A ficha aceita até 100 blocos.";
            foreach (var block in page.Blocks)
            {
                if (block is null || block.Id is null || !IdPattern.IsMatch(block.Id) || !blockIds.Add(block.Id)
                    || block.Span is < 1 or > 12 || block.Kind is not ("identity" or "portrait" or "resource" or "field" or "note" or "inventory" or "abilities" or "history")
                    || block.Title?.Length > 80 || block.Text?.Length > 2000 || block.Style is not (null or "panel" or "plain" or "accent"))
                    return "Bloco da ficha inválido ou repetido.";
                if (block.Kind == "field" && (block.Key is null || !fieldKeys.Contains(block.Key)))
                    return "Um bloco aponta para um campo inexistente.";
                if (block.Kind == "resource" && block.Key is not ("health" or "sanity" or "mana"))
                    return "Recurso da ficha inválido.";
                if (block.Kind == "note" && string.IsNullOrWhiteSpace(block.Text))
                    return "Bloco de texto vazio.";
                if ((block.Kind is "inventory" or "abilities" or "history") && block.Span < 6)
                    return "Blocos de coleção precisam de pelo menos metade da largura.";
            }
        }
        return null;
    }
}
