using System.Text.Json;

namespace Chronicles.Domain;

public sealed record RPGSystemManifest(string Id, string Name, string Version, string SchemaVersion);
public sealed record RPGSystemPlugin(RPGSystemManifest Manifest, string RulesetId, string RendererId,
    int[] SchemaVersions, int[] ExecutableSchemaVersions, string[] RequiredFields, string[] RuleRequiredFields);

public interface IRpgRulesPlugin
{
    string RulesetId { get; }
    RuleView Describe(SystemDefinition system, CharacterSheetState state);
    RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int, int> draw);
    string? ValidateStateChange(SystemDefinition system, Dictionary<string, JsonElement> before,
        Dictionary<string, JsonElement> after, bool master, bool ruleExecution, bool newCharacter);
}

// Only compiled, reviewed implementations are admitted. No assembly loading or user code execution.
public sealed class RpgPluginRegistry
{
    public static RpgPluginRegistry Default { get; } = BuiltIn();
    private readonly RPGSystemPlugin[] plugins;
    private readonly Dictionary<string, IRpgRulesPlugin> rules;
    public IReadOnlyList<RPGSystemPlugin> Plugins => Array.AsReadOnly(plugins);

    public RpgPluginRegistry(IEnumerable<RPGSystemPlugin> definitions, IEnumerable<IRpgRulesPlugin> executors)
    {
        plugins = definitions.ToArray();
        if (plugins.Select(p => (p.Manifest.Id, p.Manifest.Version)).Distinct().Count() != plugins.Length)
            throw new ArgumentException("Duplicate system and edition in plugin registry.");
        rules = executors.ToDictionary(p => p.RulesetId, StringComparer.Ordinal);
        foreach (var p in plugins)
            if (p.ExecutableSchemaVersions.Length > 0 && !rules.ContainsKey(p.RulesetId))
                throw new ArgumentException($"Missing rules executor: {p.RulesetId}.");
    }

    public RPGSystemPlugin? Resolve(SystemDefinition system) => plugins.SingleOrDefault(p =>
        p.Manifest.Id == system.TemplateId && p.Manifest.Version == system.RulesetVersion &&
        p.SchemaVersions.Contains(system.TemplateVersion));

    public IRpgRulesPlugin? RulesFor(SystemDefinition system)
    {
        var p = Resolve(system);
        return p is not null && p.ExecutableSchemaVersions.Contains(system.TemplateVersion) &&
            p.RuleRequiredFields.All(key => system.Fields.Any(f => f.Key == key && f.Type == "table"))
            ? rules.GetValueOrDefault(p.RulesetId) : null;
    }

    public static RpgPluginRegistry BuiltIn()
    {
        using var stream = typeof(RpgPluginRegistry).Assembly.GetManifestResourceStream("Chronicles.Domain.RpgSystems.builtin-rpg-plugins.json")
            ?? throw new InvalidOperationException("Missing built-in plugin catalog.");
        var catalog = JsonSerializer.Deserialize<RPGSystemPlugin[]>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Invalid built-in plugin catalog.");
        return new(catalog, [
            new CompiledRulesPlugin("ordem_paranormal:1.3"),
            new CompiledRulesPlugin("dnd_5e:2014"),
            new CompiledRulesPlugin("tormenta20:jogo-do-ano-2024-v1.3"),
            new CompiledRulesPlugin("call_of_cthulhu_7e:7e")
        ]);
    }
}

// Compatibility adapter around the existing, regression-tested edition engines.
// New plugins can provide IRpgRulesPlugin directly without modifying this adapter.
public sealed class CompiledRulesPlugin(string rulesetId) : IRpgRulesPlugin
{
    public string RulesetId { get; } = rulesetId;
    public RuleView Describe(SystemDefinition system, CharacterSheetState state) => SheetRuleActions.Describe(system, state);
    public RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int, int> draw) =>
        SheetRuleActions.Apply(system, state, command, draw);
    public string? ValidateStateChange(SystemDefinition system, Dictionary<string, JsonElement> before,
        Dictionary<string, JsonElement> after, bool master, bool ruleExecution, bool newCharacter) =>
        SheetRuleActions.ValidateStateAccess(system, before, after, master, ruleExecution, newCharacter);
}
