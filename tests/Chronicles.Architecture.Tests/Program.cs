using System.Text.Json;
using System.Xml.Linq;
using Chronicles.Domain;
using Chronicles.Application.CharacterSheets;

static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "compose.yaml"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Run from the repository.");
var modules = Path.Combine(root.FullName, "api/Chronicles.Api/modules");
foreach (var assembly in new[] { typeof(CharacterSheetState).Assembly, typeof(ExecuteCharacterRule).Assembly })
    Check(!assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("Microsoft.") || a.Name.Contains("Chronicles.Api") || a.Name.Contains("Infrastructure")), "Inner assembly references infrastructure/framework.");
foreach (var name in new[] { "Chronicles.Domain", "Chronicles.Application" })
{
    var xml = XDocument.Load(Path.Combine(modules, name, name + ".csproj"));
    Check(!xml.Descendants("PackageReference").Any() && !xml.Descendants("FrameworkReference").Any(), "Framework package in inner layer.");
    if (name == "Chronicles.Domain") Check(!xml.Descendants("ProjectReference").Any(), "Domain must have no project dependencies.");
}
var projects = Directory.GetFiles(Path.Combine(root.FullName, "api"), "*.csproj", SearchOption.AllDirectories);
var edges = projects.ToDictionary(Path.GetFullPath, p => XDocument.Load(p).Descendants("ProjectReference")
    .Select(x => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(p)!, x.Attribute("Include")!.Value))).ToArray());
void Visit(string project, HashSet<string> stack)
{
    Check(stack.Add(project), "Circular project reference: " + project);
    foreach (var target in edges.GetValueOrDefault(project) ?? []) Visit(target, stack);
    stack.Remove(project);
}
foreach (var project in projects) Visit(project, []);

var registry = RpgPluginRegistry.BuiltIn();
var stateField = new SystemField("estado_regras", "Estado", "table", "Regras", null, null, null,
    Columns: [new("chave", "Chave", "text"), new("valor", "Valor", "number")]);
var schema = new SystemDefinition([stateField, new("nex", "NEX", "number", "Agente", 5, 99, null)], null, "ordem_paranormal", 7, "1.3");
Check(registry.Resolve(schema)?.RendererId == "ordem_paranormal" && registry.RulesFor(schema) is not null, "OP registered by stable ID + edition + schema.");
Check(registry.Resolve(schema with { RulesetVersion = "1.4" }) is null, "Unknown edition must fail closed.");
Check(registry.Resolve(schema with { TemplateVersion = 900 }) is null, "Unknown schema must fail closed.");
Check(registry.RulesFor(schema with { TemplateVersion = 5 }) is null, "Legacy rendering must not enable newer rules.");
Check(registry.RulesFor(schema with { Fields = [] }) is null, "Required rules state must be present.");
Check(registry.RulesFor(new([stateField], null, "dnd_5e", 1, "2024")) is null, "2024 has no 2014 executor.");
try { _ = new RpgPluginRegistry([registry.Plugins[0], registry.Plugins[0]], []); throw new Exception("Accepted duplicate plugin"); }
catch (ArgumentException) { }

var actor = Guid.NewGuid();
var state = new CharacterSheetState("Agente de teste", null, null, 20, 30, 40, 50, 10, 20,
    new() { ["nex"] = JsonSerializer.SerializeToElement(35) }, 4);
var initial = new CharacterSheetSnapshot(Guid.NewGuid(), Guid.NewGuid(), actor, "player", state, schema, DateTime.UtcNow, Guid.NewGuid());
var store = new MemoryRuleStore(initial);
var random = new FixedRandom();
var command = new ExecuteCharacterRule(store, registry, random);
var accepted = await command.Execute(initial.Id, actor, 4, new("damage", 3));
Check(accepted.Failure is null && accepted.Character?.State.Health == 17 && store.Commits == 1, "Real OP command invokes plugin and persistence port: " + JsonSerializer.Serialize(accepted));
var stale = await command.Execute(initial.Id, actor, 4, new("heal", 1));
Check(stale.Failure == RuleFailure.Conflict && store.Commits == 1, "Stale versions do not mutate state.");
Check((await command.Execute(initial.Id, actor, 5, new("configure", 1, "sorte_opcional"))).Failure == RuleFailure.Forbidden, "GM-only option enforced in application.");
Check((await command.Execute(initial.Id, actor, 5, new("damage", 1, Visibility: "gm"))).Failure == RuleFailure.Forbidden, "Private GM visibility enforced in application.");
Check((await command.Execute(initial.Id, Guid.NewGuid(), 5, new("damage", 1))).Failure == RuleFailure.NotFound, "Store port hides inaccessible identifiers.");
store.Current = store.Current with { Role = "spectator" };
Check((await command.Execute(initial.Id, actor, 5, new("damage", 1))).Failure == RuleFailure.Forbidden, "Spectator cannot modify own sheet.");
store.Current = store.Current with { Role = "gm" };
store.Conflict = true;
Check((await command.Execute(initial.Id, actor, 5, new("damage", 1))).Failure == RuleFailure.Conflict, "Commit-time race produces conflict.");

// Prove a genuinely new compiled system does not require the legacy rule switch or a fixed data structure.
var custom = new RPGSystemPlugin(new("custom_dice", "Fixture", "v1", "2"), "custom-dice-v1", "custom_presentation", [2], [2], [], []);
var extensions = new RpgPluginRegistry([custom], [new CustomPlugin()]);
store.Conflict = false;
store.Current = initial with { Role = "gm", System = new([], null, "custom_dice", 2, "v1") };
var extended = await new ExecuteCharacterRule(store, extensions, random).Execute(initial.Id, actor, 4, new("custom"));
Check(extended.Result?.Character.Fields?["custom_counter"].GetInt32() == 7, "New plugin can introduce its own fields and renderer identifier.");
store.Current = initial with { Role = "gm", System = new([], null, "custom_dice", 2, "v1") };
var beforeInvalid = store.Commits;
var invalidPlugin = new RpgPluginRegistry([custom], [new CustomPlugin(8)]);
Check((await new ExecuteCharacterRule(store, invalidPlugin, random).Execute(initial.Id, actor, 4, new("custom"))).Failure == RuleFailure.Invalid &&
    store.Commits == beforeInvalid, "Plugin-specific invariants run before commit.");
await CharacterWriteTests.Run();
Console.WriteLine("Architecture: independent inner assemblies, acyclic project references, versioned registry, OP use case, access, conflicts and a new plugin passed.");

sealed class FixedRandom : IGameRandom { public int Draw(int sides) => sides; }
sealed class MemoryRuleStore(CharacterSheetSnapshot initial) : ICharacterRuleStore
{
    public CharacterSheetSnapshot Current = initial;
    public int Commits;
    public bool Conflict;
    public Task<CharacterSheetSnapshot?> Load(Guid id, Guid actor, CancellationToken ct) => Task.FromResult<CharacterSheetSnapshot?>(id == Current.Id && actor == Current.OwnerId ? Current : null);
    public Task<CharacterSheetSnapshot?> Commit(CharacterRuleChange change, CancellationToken ct)
    {
        if (Conflict) return Task.FromResult<CharacterSheetSnapshot?>(null);
        Commits++;
        Current = Current with { State = change.Result.Character with { Version = Current.State.Version + 1 } };
        return Task.FromResult<CharacterSheetSnapshot?>(Current);
    }
}
sealed class CustomPlugin(int counter = 7) : IRpgRulesPlugin
{
    public string RulesetId => "custom-dice-v1";
    public RuleView Describe(SystemDefinition system, CharacterSheetState state) => new("v1", [], [], [], []);
    public RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int, int> draw) =>
        new(state with { Fields = new() { ["custom_counter"] = JsonSerializer.SerializeToElement(counter) } }, "custom", []);
    public string? ValidateStateChange(SystemDefinition system, Dictionary<string, JsonElement> before,
        Dictionary<string, JsonElement> after, bool master, bool ruleExecution, bool newCharacter) =>
        after.TryGetValue("custom_counter", out var value) && value.GetInt32() != 7 ? "Invalid custom counter" : null;
}
