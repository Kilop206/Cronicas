using System.Text.Json;
using static Chronicles.Api.CharacterEndpoints;

namespace Chronicles.Api;

public sealed record RuleCommand(string Action, int Amount = 0, string? Option = null, string Visibility = "campaign", string? Expression = null);
public sealed record RuleChoice(string Value, string Label);
public sealed record RuleAction(string Id, string Label, string? AmountLabel = null, RuleChoice[]? Options = null, bool Hidden = false);
public sealed record RuleStat(string Label, string Value);
public sealed record RuleSetting(string Key, string Label, int Min = 0, int Max = 9999);
public sealed record RuleView(string Edition, RuleAction[] Actions, RuleStat[] Stats, RuleSetting[] Settings, Dictionary<string, int> State, Dictionary<string, RuleCheckPreview>? Checks = null);
public sealed record RuleCheckPreview(string? Expression, string? Error);
public sealed record RuleResult(CharacterInput Character, string Message, RuleRoll[] Rolls);
public sealed record RuleRoll(string Expression, int[] Rolls, int[] KeptIndexes, int Modifier, int Total);

// Pure, edition-specific actions. No database, HTTP or arbitrary expressions here.
public static class SheetRuleActions
{
    public const string StateKey = "estado_regras";
    public static bool Supports(SystemDefinition system) => system.Fields.Any(f => f.Key == StateKey && f.Type == "table") &&
        (system.TemplateId, system.TemplateVersion, system.RulesetVersion) switch
        {
            ("ordem_paranormal", 6 or 7, "1.3") or ("dnd_5e", 3 or 4, "2014") or
            ("tormenta20", 3 or 4, "jogo-do-ano-2024-v1.3") or ("call_of_cthulhu_7e", 4 or 5, "7e") => true,
            _ => false
        };

    public static RuleView Describe(SystemDefinition system, CharacterInput input)
    {
        var s = new RuleState(input);
        if (!Supports(system)) throw new ArgumentException("Atualize o modelo para usar as ações desta edição.");
        var view = system.TemplateId switch
        {
            "ordem_paranormal" => ParanormalRules.Describe(s),
            "dnd_5e" => Dnd2014Rules.Describe(s),
            "tormenta20" => TormentaRules.Describe(s),
            _ => CthulhuRules.Describe(s)
        };
        var weapon = WeaponActions.Describe(system, s);
        if (system.Fields.Any(f => f.Key == WeaponActions.FieldKey))
        {
            if (system.TemplateId == "dnd_5e") view = view with { Settings = [.. view.Settings, new("modo_ataque", "Ataques: −1 desvantagem, 0 normal, 1 vantagem", -1, 1)] };
            if (system.TemplateId == "call_of_cthulhu_7e") view = view with { Settings = [.. view.Settings, new("dados_ataque", "Ataques: dados de bônus (+) ou penalidade (−)", -2, 2)] };
        }
        RuleAction[] weaponActions = weapon is null ? [] : [weapon];
        if (system.TemplateId is "ordem_paranormal" or "tormenta20")
        {
            var paranormal = system.TemplateId == "ordem_paranormal";
            view = view with { Actions = [.. view.Actions, .. D20Conditions.Actions(paranormal), D20Checks.Action(system)], Stats = [.. view.Stats, .. D20Conditions.Stats(s, paranormal)],
                Checks = system.Fields.Where(f => f.Type == "number" && f.RollExpression is not null).ToDictionary(f => f.Key, f => D20Checks.Preview(system, s, f.Key)),
                Settings = [.. view.Settings, new("sentido_percepcao", "Percepção: 0 geral, 1 visão, 2 audição", 0, 2), new("arma_leve", "Arma atual é leve (0 ou 1)", 0, 1), new("atletismo_natacao", "Atletismo atual é natação (0 ou 1)", 0, 1),
                    new("camuflagem_alvo", "Camuflagem do alvo: 0 nenhuma, 1 parcial, 2 total", 0, 2), new("ignora_camuflagem", "Ataque atual ignora camuflagem (0 ou 1)", 0, 1),
                    .. (paranormal ? new RuleSetting[] { new("dados_zero_penalidade", "Atributo zero com penalidade: dados definidos pela mesa (0: não configurado)", 0, 40) } : [])] };
        }
        if (system.TemplateId == "dnd_5e") view = view with { Actions = [.. view.Actions, .. Dnd2014Conditions.Actions(system)], Stats = [.. view.Stats, .. Dnd2014Conditions.Stats(s)],
            Settings = [.. view.Settings, new("modo_testes", "Testes: −1 desvantagem, 0 normal, 1 vantagem", -1, 1), new("fonte_medo_oculta", "Fonte do medo fora de vista (0 ou 1)", 0, 1), new("alvo_ve_invisivel", "Alvo consegue ver invisibilidade (0 ou 1)", 0, 1), new("alvo_encantador", "Alvo é a fonte do encanto (0 ou 1)", 0, 1), new("sentido_percepcao", "Percepção: 0 geral, 1 visão, 2 audição", 0, 2), new("vantagem_concentracao", "Concentração: −1 desvantagem, 0 normal, 1 vantagem", -1, 1)] };
        return view with { Actions = [.. view.Actions, .. weaponActions, new RuleAction("configure", "Salvar configuração", "Valor", view.Settings.Select(x => new RuleChoice(x.Key, x.Label)).ToArray())] };
    }

    public static RuleResult Apply(SystemDefinition system, CharacterInput input, RuleCommand command, Func<int, int> draw)
    {
        var view = Describe(system, input);
        var action = view.Actions.SingleOrDefault(a => a.Id == command.Action) ?? throw new ArgumentException("Ação indisponível nesta edição.");
        if (command.Amount > 9999 || command.Amount < (command.Action == "configure" ? -9999 : 0) || command.Visibility is not ("campaign" or "private" or "gm")) throw new ArgumentException("Valor ou visibilidade inválidos.");
        if (action.Options is not null && !action.Options.Any(o => o.Value == command.Option)) throw new ArgumentException("Escolha uma opção válida.");
        var s = new RuleState(input, draw);
        if (command.Action == "configure")
        {
            var setting = view.Settings.Single(x => x.Key == command.Option);
            RuleState.Require(command.Amount >= setting.Min && command.Amount <= setting.Max, $"Informe um valor entre {setting.Min} e {setting.Max}.");
            s.Set(setting.Key, command.Amount);
            var totalLevels = s.State.Where(x => x.Key.StartsWith("nivel_", StringComparison.Ordinal)).Sum(x => x.Value);
            RuleState.Require(totalLevels <= 20, "A soma dos níveis não pode ultrapassar 20.");
            if (setting.Key.StartsWith("nivel_", StringComparison.Ordinal) && totalLevels > 0) s.Field("nivel", totalLevels);
            if (system.TemplateId == "dnd_5e" && (setting.Key.StartsWith("nivel_", StringComparison.Ordinal) || setting.Key.StartsWith("terco_", StringComparison.Ordinal))) Dnd2014Rules.SyncSlots(s);
            return new(s.Export(), "Configuração salva.", []);
        }
        if (command.Action == "attack")
        {
            var attackMessage = WeaponActions.Apply(system, s, command);
            return new(s.Export(), attackMessage, s.Rolls.ToArray());
        }
        if (system.TemplateId == "dnd_5e" && Dnd2014Conditions.Handles(command.Action))
        {
            var conditionMessage = Dnd2014Conditions.Apply(system, s, command);
            return new(s.Export(), conditionMessage, s.Rolls.ToArray());
        }
        if (system.TemplateId is "ordem_paranormal" or "tormenta20")
        {
            if (D20Conditions.Handles(command.Action)) { var conditionMessage = D20Conditions.Apply(s, command); return new(s.Export(), conditionMessage, s.Rolls.ToArray()); }
            if (command.Action == "test") { var testMessage = D20Checks.Test(system, s, command); return new(s.Export(), testMessage, s.Rolls.ToArray()); }
        }
        var message = system.TemplateId switch
        {
            "ordem_paranormal" => ParanormalRules.Apply(s, command),
            "dnd_5e" => Dnd2014Rules.Apply(s, command),
            "tormenta20" => TormentaRules.Apply(system, s, command),
            _ => CthulhuRules.Apply(s, command)
        };
        return new(s.Export(), message, s.Rolls.ToArray());
    }

    public static string? ValidateStateAccess(SystemDefinition system, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after, bool master)
    {
        if (!Supports(system)) return null;
        try
        {
            var empty = new CharacterInput("Estado", null, null, 1, 1, 1, 1, 1, 1);
            var next = new RuleState(empty with { Fields = after });
            var old = new RuleState(empty with { Fields = before });
            foreach (var key in new[] { "morto", "insano", "estavel", "morrendo", "enlouquecendo", "ferimento_grave", "inconsciente", "insanidade_temporaria", "insanidade_indefinida", "sangrando", "dormiu", "relaxou" })
                if (next.Get(key) is < 0 or > 1) return $"Indicador inválido: {key}.";
            if (system.TemplateId == "dnd_5e" && (next.Get("exaustao") is < 0 or > 6)) return "Exaustão fora dos limites.";
            if (system.TemplateId is "ordem_paranormal" or "tormenta20")
                foreach (var pair in next.State.Where(x => x.Key.StartsWith("cond_", StringComparison.Ordinal)))
                    if (D20Conditions.Choices(system.TemplateId == "ordem_paranormal").Any(c => "cond_" + c.Value == pair.Key) && pair.Value is < -1 or > 10000) return "Duração de condição inválida.";
            if (!master && next.Get("sorte_opcional") != old.Get("sorte_opcional")) return "Somente o mestre pode alterar a regra opcional de sorte.";
            var settings = Describe(system, empty with { Fields = after }).Settings;
            foreach (var setting in settings)
                if (next.Get(setting.Key) < setting.Min || next.Get(setting.Key) > setting.Max) return $"Configuração fora dos limites: {setting.Label}.";
            if (next.State.Where(x => x.Key.StartsWith("nivel_", StringComparison.Ordinal)).Sum(x => x.Value) > 20) return "A soma dos níveis não pode ultrapassar 20.";
            return null;
        }
        catch (ArgumentException error) { return error.Message; }
    }
}

public sealed class RuleState
{
    private CharacterInput character;
    private readonly Func<int, int> draw;
    public Dictionary<string, JsonElement> Fields { get; }
    public Dictionary<string, int> State { get; } = [];
    public List<RuleRoll> Rolls { get; } = [];
    public RuleState(CharacterInput input, Func<int, int>? draw = null)
    {
        character = input;
        this.draw = draw ?? (_ => throw new InvalidOperationException("Esta ação precisa de dados."));
        Fields = input.Fields?.ToDictionary(x => x.Key, x => x.Value.Clone()) ?? [];
        if (!Fields.TryGetValue(SheetRuleActions.StateKey, out var rows)) return;
        if (rows.ValueKind != JsonValueKind.Array) throw new ArgumentException("Estado de regras inválido.");
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("chave", out var key) || key.ValueKind != JsonValueKind.String ||
                !row.TryGetProperty("valor", out var value) || !value.TryGetInt32(out var number) || number is < -10000 or > 10000 ||
                key.GetString() is not { Length: > 0 and <= 64 } name || !State.TryAdd(name, number)) throw new ArgumentException("Estado de regras inválido ou duplicado.");
        }
    }
    public int Health { get => character.Health; set => character = character with { Health = Math.Clamp(value, 0, character.MaxHealth) }; }
    public int Mana { get => character.Mana; set => character = character with { Mana = Math.Clamp(value, 0, character.MaxMana) }; }
    public int Sanity { get => character.Sanity; set => character = character with { Sanity = Math.Clamp(value, 0, character.MaxSanity) }; }
    public int MaxHealth => character.MaxHealth;
    public int MaxMana => character.MaxMana;
    public int MaxSanity => character.MaxSanity;
    public int N(string key, int fallback = 0) => Fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : fallback;
    public string Text(string key) => Fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public int Get(string key, int fallback = 0) => State.GetValueOrDefault(key, fallback);
    public int ParanormalSkill(string key)
    {
        var bonus = N(key);
        if (!Fields.TryGetValue("pericias_detalhes", out var rows) || rows.ValueKind != JsonValueKind.Array) return bonus;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("chave", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() != key) continue;
            foreach (var column in new[] { "treino", "outros" }) if (row.TryGetProperty(column, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) bonus += number;
            break;
        }
        return bonus;
    }
    public void Set(string key, int value) { if (value == 0) State.Remove(key); else State[key] = value; }
    public void Field(string key, int value) => Fields[key] = JsonSerializer.SerializeToElement(value);
    public int Die(int sides)
    {
        var value = draw(sides);
        if (value < 1 || value > sides) throw new ArgumentException("Dado fora dos limites.");
        Rolls.Add(new($"1d{sides}", [value], [0], 0, value));
        return value;
    }
    public int Pool(int count, int sides)
    {
        Require(count is >= 1 and <= 80 && sides is 3 or 4 or 6 or 8 or 10 or 12 or 20 or 100, "Quantidade ou faces inválidas.");
        var values = Enumerable.Range(0, count).Select(_ => draw(sides)).ToArray();
        Require(values.All(value => value >= 1 && value <= sides), "Resultado de dado fora dos limites.");
        var total = values.Sum();
        Rolls.Add(new($"{count}d{sides}", values, Enumerable.Range(0, count).ToArray(), 0, total));
        return total;
    }
    public int Percentile(int bonus)
    {
        Require(bonus is >= -2 and <= 2, "Dados percentuais fora dos limites.");
        var result = PercentileDice.Roll(bonus, () => draw(10) - 1);
        Rolls.Add(new("d100" + (bonus == 0 ? "" : $" {bonus:+#;-#}"), result.Rolls, result.KeptIndexes, 0, result.Total));
        return result.Total;
    }
    public int Roll(string expression)
    {
        Require(DiceExpression.TryParse(expression, out var dice) && dice is not null, "Expressão de dados inválida.");
        var result = dice!.Roll(draw);
        Rolls.Add(new(expression, result.Rolls, result.KeptIndexes, dice.Modifier, result.Total));
        return result.Total;
    }
    public void SpendMana(int amount) { Require(amount > 0 && Mana >= amount, "Recurso insuficiente ou custo inválido."); Mana -= amount; }
    public void HealthMaximum(int value) { Require(value is >= 1 and <= 9999, "Total de PV inválido."); character = character with { Health = value, MaxHealth = value }; }
    public void ChangeHealthMaximum(int value) { Require(value is >= 0 and <= 9999, "Total de PV inválido."); character = character with { MaxHealth = value, Health = Math.Min(Health, value) }; }
    public void LoseSanityMaximum(int amount)
    {
        var maximum = Math.Max(0, MaxSanity - amount);
        character = character with { Sanity = Math.Min(Sanity, maximum), MaxSanity = maximum };
        if (maximum == 0) Set("insano", 1);
        Set("san_permanente_perdida", Get("san_permanente_perdida") + amount);
    }
    public void Vitals(int health, int mana, int? sanity = null)
    {
        Require(health is >= 1 and <= 9999 && mana is >= 0 and <= 9999 && sanity is not (< 0 or > 9999), "Totais de recursos inválidos.");
        character = character with { MaxHealth = health, Health = health, MaxMana = mana, Mana = mana };
        if (sanity is { } san) character = character with { MaxSanity = san, Sanity = san };
    }
    public CharacterInput Export()
    {
        Require(State.Count <= 100, "O estado excede 100 indicadores. Remova indicadores sem uso.");
        Fields[SheetRuleActions.StateKey] = JsonSerializer.SerializeToElement(State.Select(x => new { chave = x.Key, valor = x.Value }).ToArray());
        return character with { Fields = Fields };
    }
    public static void Require(bool test, string error) { if (!test) throw new ArgumentException(error); }
    public static RuleChoice[] RestOptions => [new("poor", "Precário"), new("normal", "Normal"), new("comfortable", "Confortável"), new("luxury", "Luxuoso")];
    public static int Recovery(int basic, string? option) => option switch { "poor" => basic / 2, "comfortable" => basic * 2, "luxury" => basic * 3, _ => basic };
}
