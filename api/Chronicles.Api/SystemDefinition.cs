using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chronicles.Api;

public sealed record SheetBlock(string Id, string Kind, int Span, string? Title = null, string? Key = null,
    string? Text = null, string? Style = null, string? MaxKey = null,
    double? X = null, double? Y = null, double? Width = null, double? Height = null);
public sealed record SheetPage(string Id, string Title, SheetBlock[] Blocks, string? LayoutMode = null,
    string? BackgroundUrl = null, double? AspectRatio = null);
public sealed record SheetLayout(string AccentColor, SheetPage[] Pages, string? Skin = null);
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
        if (layout.AccentColor is null || !ColorPattern.IsMatch(layout.AccentColor) || layout.Pages is null || layout.Pages.Length is < 1 or > 8
            || layout.Skin is not (null or "archive" or "investigation" or "fantasy"))
            return "Defina uma cor hexadecimal e de 1 a 8 páginas.";
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        var blockIds = new HashSet<string>(StringComparer.Ordinal);
        var fieldKeys = fields.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var numericKeys = fields.Where(x => x.Type == "number").Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var total = 0;
        foreach (var page in layout.Pages)
        {
            if (page is null || page.Id is null || !IdPattern.IsMatch(page.Id) || !pageIds.Add(page.Id)
                || page.Title?.Trim().Length is not (>= 2 and <= 60) || page.Blocks is null || page.Blocks.Length is < 1 or > 40)
                return "Página da ficha inválida ou repetida.";
            if (page.LayoutMode is not (null or "grid" or "canvas")
                || page.AspectRatio is < 0.5 or > 2
                || page.BackgroundUrl is not null && (page.BackgroundUrl.Length > 2048
                    || !Uri.TryCreate(page.BackgroundUrl, UriKind.Absolute, out var background)
                    || background.Scheme != Uri.UriSchemeHttps))
                return "Modo, proporção ou imagem de fundo da página inválidos.";
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
                if (block.Kind == "resource")
                {
                    var builtIn = block.Key is "health" or "sanity" or "mana";
                    if ((!builtIn && (block.Key is null || !numericKeys.Contains(block.Key)))
                        || (!builtIn && block.MaxKey is null)
                        || (block.MaxKey is not null && block.MaxKey is not ("maxHealth" or "maxSanity" or "maxMana") && !numericKeys.Contains(block.MaxKey))
                        || block.Key == block.MaxKey)
                        return "Recurso da ficha precisa de campos numéricos válidos para valor atual e máximo.";
                }
                if (block.Kind == "note" && string.IsNullOrWhiteSpace(block.Text))
                    return "Bloco de texto vazio.";
                if ((block.Kind is "inventory" or "abilities" or "history") && block.Span < 6)
                    return "Blocos de coleção precisam de pelo menos metade da largura.";
                if (page.LayoutMode == "canvas" && (block.X is null or < 0 or > 95
                    || block.Y is null or < 0 or > 95 || block.Width is null or < 5 or > 100
                    || block.Height is null or < 5 or > 100 || block.X + block.Width > 100
                    || block.Y + block.Height > 100))
                    return "Posição de bloco fora dos limites da página livre.";
            }
        }
        return null;
    }
}
