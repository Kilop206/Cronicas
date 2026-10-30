using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chronicles.Api;

public static partial class WeaponActions
{
    public const string FieldKey = "ataques_regras";
    [GeneratedRegex(@"\{([a-z][a-z0-9_]{0,39})\}", RegexOptions.CultureInvariant)] private static partial Regex Reference();
    private static JsonElement[] Rows(RuleState s) => s.Fields.TryGetValue(FieldKey, out var rows) && rows.ValueKind == JsonValueKind.Array ? rows.EnumerateArray().ToArray() : [];
    private static string Text(JsonElement row, string key, string fallback = "") => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : fallback;
    private static int Number(JsonElement row, string key, int fallback = 0) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;
    private static bool Flag(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    public static RuleAction? Describe(SystemDefinition system, RuleState s)
    {
        if (!system.Fields.Any(field => field.Key == FieldKey)) return null;
        var choices = Rows(s).Select((row, i) => new RuleChoice(i.ToString(), Text(row, "nome"))).Where(choice => choice.Label.Length > 0).ToArray();
        return choices.Length == 0 ? null : new("attack", "Atacar e rolar dano", system.TemplateId == "call_of_cthulhu_7e" ? "Dificuldade: 1 regular, 2 difícil, 5 extrema" : "Defesa do alvo (0: só rolar)", choices);
    }
    public static string Apply(SystemDefinition system, RuleState s, RuleCommand command)
    {
        RuleState.Require(s.Get("morto") == 0, "Personagens mortos não podem atacar.");
        var rows = Rows(s);
        RuleState.Require(int.TryParse(command.Option, out var index) && index >= 0 && index < rows.Length, "Ataque não encontrado.");
        var row = rows[index]; var name = Text(row, "nome");
        var ammo = Number(row, "municao", -1);
        RuleState.Require(ammo is >= -1 and <= 9999 && ammo != 0, "Munição esgotada; use −1 para ataques sem limite de munição.");
        var damageFormula = Text(row, "dano", "1"); var extra = Text(row, "dano_extra", "0");
        if (system.TemplateId == "call_of_cthulhu_7e" && Text(row, "tipo", "corpo") == "corpo" && string.IsNullOrWhiteSpace(Text(row, "dano_extra"))) extra = CthulhuRules.DamageBonus(s.N("forca"), s.N("tamanho"));
        DamageFormula.Parse(damageFormula, s); DamageFormula.Parse(extra, s);
        string message;
        if (system.TemplateId == "call_of_cthulhu_7e") message = Cthulhu(s, row, command, name, damageFormula, extra);
        else
        {
            var dnd = system.TemplateId == "dnd_5e";
            var baseKey = Text(row, "base", dnd ? "mod_forca" : "per_luta");
            RuleState.Require(system.Fields.Any(f => f.Key == baseKey && f.Type == "number"), "Escolha a chave de um atributo, modificador ou perícia numérica do sistema.");
            string expression;
            if (system.TemplateId == "ordem_paranormal")
            {
                var field = system.Fields.Single(f => f.Key == baseKey);
                RuleState.Require(field.RollExpression is not null, "A perícia base precisa de rolagem configurada.");
                expression = Resolve(field.RollExpression!, s, true);
            }
            else
            {
                var bonus = s.N(baseKey) + Number(row, "bonus");
                if (dnd && Flag(row, "treinado")) bonus += s.N("proficiencia");
                var mode = dnd ? s.Get("modo_ataque") : 0;
                RuleState.Require(mode is >= -1 and <= 1, "Modo de ataque inválido.");
                expression = (mode == 0 ? "1d20" : mode > 0 ? "2d20kh1" : "2d20kl1") + (bonus < 0 ? bonus.ToString() : "+" + bonus);
            }
            var total = s.Roll(expression); var test = s.Rolls[^1]; var natural = test.Rolls[test.KeptIndexes[0]];
            var hit = natural == 20 || (natural != 1 || system.TemplateId == "ordem_paranormal") && (command.Amount == 0 || total >= command.Amount);
            var margin = dnd ? 20 : Number(row, "margem", 20); var multiplier = dnd ? 2 : Number(row, "multiplicador", 2);
            RuleState.Require(margin is >= 2 and <= 20 && multiplier is >= 2 and <= 5, "Margem ou multiplicador de crítico inválidos.");
            var critical = hit && natural >= margin;
            if (!hit) message = $"{name}: {expression} = {total}; errou. Munição consumida quando aplicável.";
            else
            {
                var damage = Math.Max(0, DamageFormula.Evaluate(damageFormula, s, critical ? multiplier : 1) + DamageFormula.Evaluate(extra, s, critical && dnd ? 2 : 1));
                message = $"{name}: {expression} = {total}{(critical ? " · crítico" : "")}; dano {damage}. " + (command.Amount == 0 ? "Defesa não informada; confirme o acerto antes de aplicar." : "Aplique ao alvo após resistências e imunidades.");
            }
        }
        if (ammo > 0)
        {
            var values = rows.Select(r => r.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())).ToArray();
            values[index]["municao"] = JsonSerializer.SerializeToElement(ammo - 1);
            s.Fields[FieldKey] = JsonSerializer.SerializeToElement(values);
        }
        return message;
    }
    private static string Cthulhu(RuleState s, JsonElement row, RuleCommand command, string name, string damage, string extra)
    {
        RuleState.Require(command.Amount is 1 or 2 or 5, "Informe dificuldade 1, 2 ou 5.");
        var key = Text(row, "base", "per_lutar");
        RuleState.Require(s.Fields.TryGetValue(key, out var skill) && skill.ValueKind == JsonValueKind.Number, "Perícia percentual não encontrada.");
        var baseValue = s.N(key); var threshold = baseValue / command.Amount;
        var roll = s.Percentile(s.Get("dados_ataque"));
        var malfunction = Number(row, "falha", 100);
        RuleState.Require(Text(row, "tipo") != "distancia" || malfunction is >= 1 and <= 100, "Informe mau funcionamento entre 1 e 100.");
        if (Text(row, "tipo") == "distancia" && roll >= malfunction) return $"{name}: {roll}; mau funcionamento da arma. Munição consumida.";
        var fumble = roll == 100 || threshold < 50 && roll >= 96;
        if (fumble || roll > threshold && roll != 1) return $"{name}: {roll} / {threshold}; {(fumble ? "falha crítica" : "falha")}.";
        var extreme = (roll == 1 || roll <= baseValue / 5) && (Text(row, "tipo") != "distancia" || command.Amount < 5);
        var total = extreme ? DamageFormula.Evaluate(damage, s, maximum: true) + DamageFormula.Evaluate(extra, s, maximum: true)
            + (Flag(row, "empala") ? DamageFormula.Evaluate(damage, s) : 0)
            : DamageFormula.Evaluate(damage, s) + DamageFormula.Evaluate(extra, s);
        return $"{name}: {roll} / {threshold}{(extreme ? Flag(row, "empala") ? " · empalamento" : " · dano máximo" : "")}; dano {Math.Max(0, total)}. Confirme a oposição em combate corpo a corpo antes de aplicar ao alvo.";
    }
    public static string Resolve(string source, RuleState state, bool paranormal)
    {
        if (paranormal)
        {
            var count = Regex.Match(source, @"^\{([a-z][a-z0-9_]*)\}d20kh1");
            if (count.Success && state.N(count.Groups[1].Value) == 0) source = source.Replace(count.Value, "2d20kl1", StringComparison.Ordinal);
        }
        var result = Reference().Replace(source, m => { var key = m.Groups[1].Value; RuleState.Require(state.Fields.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number, "Referência numérica de teste desconhecida."); return (paranormal && key.StartsWith("per_", StringComparison.Ordinal) ? state.ParanormalSkill(key) : state.N(key)).ToString(); }).Replace("+-", "-");
        result = Regex.Replace(result, @"^1d20(?:kh1|kl1)", "1d20");
        RuleState.Require(DiceExpression.TryParse(result, out _), "Expressão de ataque inválida.");
        return result;
    }
}
