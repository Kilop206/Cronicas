using System.Globalization;
using System.Text.Json;
using Chronicles.Domain;

namespace Chronicles.Application.Characters;

public static class CharacterOverlayProjection
{
    public static CharacterOverlayDocument Document(CharacterOverlaySource source, RpgPluginRegistry plugins)
    {
        var s = source.State; var definition = source.System; var values = s.Fields ?? [];
        var renderer = definition is null || definition.RendererMode == "layout" ? null : plugins.Resolve(definition)?.RendererId;
        var custom = renderer is not ("ordem_paranormal" or "dnd_5e" or "tormenta20" or "call_of_cthulhu_7e");
        var resourceKeys = renderer switch
        {
            "ordem_paranormal" => new[] { (Key: "health", Label: "PV"), (Key: "mana", Label: "PE"), (Key: "sanity", Label: "SAN") },
            "dnd_5e" => [("health", "HP")],
            "tormenta20" => [("health", "PV"), ("mana", "PM")],
            "call_of_cthulhu_7e" => [("health", "HP"), ("sanity", "SAN"), ("mana", "Magic Points")],
            _ => definition?.Layout is { } layout
                ? layout.Pages.SelectMany(p => p.Blocks).Where(b => b.Kind == "resource" && b.Key is "health" or "sanity" or "mana")
                    .GroupBy(b => b.Key).Select(g => (Key: g.Key!, Label: g.First().Title ?? "Recurso")).ToArray()
                : [("health", "Vida"), ("sanity", "Sanidade"), ("mana", "Mana")]
        };
        var indicatorKeys = renderer switch
        {
            "ordem_paranormal" => new[] { (Key: "nex", Label: "NEX"), (Key: "defesa", Label: "Defesa") },
            "dnd_5e" => [("nivel", "Nível"), ("classe_armadura", "AC"), ("iniciativa", "Iniciativa")],
            "tormenta20" => [("nivel", "Nível"), ("defesa", "Defesa")],
            "call_of_cthulhu_7e" => [("sorte", "Sorte")],
            _ => definition?.Layout is { } layout
                ? layout.Pages.SelectMany(p => p.Blocks).Where(b => b.Kind == "field" && b.Style == "score" && b.Key is not null)
                    .GroupBy(b => b.Key).Select(g => (Key: g.Key!, Label: g.First().Title ?? definition.Fields.FirstOrDefault(f => f.Key == g.Key)?.Label ?? "Indicador")).Take(6).ToArray()
                : []
        };
        var resources = custom && definition?.Layout is { } customLayout
            ? customLayout.Pages.SelectMany(p => p.Blocks).Where(b => b.Kind is "resource" or "tracker" && b.Key is not null)
                .GroupBy(b => b.Key!).Select(g => g.First()).Select(b => new CharacterOverlayResource(
                    b.Title ?? definition.Fields.FirstOrDefault(f => f.Key == b.Key)?.Label ?? "Recurso", Value(s, values, b.Key!),
                    b.MaxKey is { } max ? Value(s, values, max) : Maximum(s, b.Key!))).ToArray()
            : resourceKeys.Select(r => new CharacterOverlayResource(r.Label, Value(s, values, r.Key), Maximum(s, r.Key))).ToArray();
        var conditions = definition is not null && plugins.RulesFor(definition) is CompiledRulesPlugin &&
            definition.TemplateId is "ordem_paranormal" or "tormenta20" ? new RuleState(s) : null;
        var indicators = indicatorKeys.Where(i => values.ContainsKey(i.Key)).Select(i => new CharacterOverlayIndicator(i.Label,
            i.Key == "defesa" && conditions is not null
                ? D20Conditions.Defense(conditions, false, definition!.TemplateId == "ordem_paranormal").ToString(CultureInfo.InvariantCulture)
                : values[i.Key].ToString())).ToArray();
        return new(s.Name, s.PortraitUrl, s.BackgroundUrl, s.Health, s.MaxHealth, s.Sanity, s.MaxSanity, s.Mana, s.MaxMana,
            source.UpdatedAt, definition?.TemplateId, resources, indicators);
    }

    private static int Value(CharacterSheetState s, Dictionary<string, JsonElement> fields, string key) => key switch
    { "health" => s.Health, "sanity" => s.Sanity, "mana" => s.Mana, _ => fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var number) ? number : 0 };
    private static int Maximum(CharacterSheetState s, string key) => key switch
    { "health" => s.MaxHealth, "sanity" => s.MaxSanity, "mana" => s.MaxMana, _ => 1 };
}
