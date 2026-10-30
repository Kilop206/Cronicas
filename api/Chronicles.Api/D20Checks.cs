using System.Text.RegularExpressions;

namespace Chronicles.Api;

// The schema supplies stable roll keys and attribute dependencies, never editable labels.
public static partial class D20Checks
{
    private static readonly string[] OpAttributes = ["agilidade", "forca", "intelecto", "presenca", "vigor"];
    private static readonly string[] T20Attributes = ["forca", "destreza", "constituicao", "inteligencia", "sabedoria", "carisma"];
    [GeneratedRegex(@"^\{([a-z][a-z0-9_]*)\}d20(?:kh1)?")] private static partial Regex OpAttribute();
    [GeneratedRegex(@"\b[a-z][a-z0-9_]*\b")] private static partial Regex Identifiers();
    public static RuleAction Action(SystemDefinition system) => new("test", "Realizar teste", "DT/CD (0: só rolar)", system.Fields.Where(f => f.Type == "number" && f.RollExpression is not null).Select(f => new RuleChoice(f.Key, f.Label)).ToArray());
    public static RuleCheckPreview Preview(SystemDefinition system, RuleState s, string key)
    {
        if (key == "per_reflexos" && D20Conditions.Has(s, "indefeso")) return new(null, "Falha automática por indefeso.");
        try { return new(Expression(system, s, key), null); }
        catch (ArgumentException error) { return new(null, error.Message); }
    }
    public static string Attribute(SystemDefinition system, string key, bool paranormal)
    {
        var field = system.Fields.SingleOrDefault(f => f.Key == key);
        if (field is null) return "";
        if ((paranormal ? OpAttributes : T20Attributes).Contains(key)) return key;
        if (paranormal) return OpAttribute().Match(field.RollExpression ?? "").Groups[1].Value;
        // A custom formula may name a different attribute; use that declared dependency.
        var attributes = Identifiers().Matches(field.Formula ?? "").Select(m => m.Value).Where(T20Attributes.Contains).Distinct().ToArray();
        return attributes.Length == 1 ? attributes[0] : "";
    }
    public static string Expression(SystemDefinition system, RuleState s, string key, bool attack = false, bool ranged = false)
    {
        var paranormal = system.TemplateId == "ordem_paranormal";
        var field = system.Fields.SingleOrDefault(f => f.Key == key && f.Type == "number");
        RuleState.Require(field is not null && (field.RollExpression is not null || attack && !paranormal), "O campo precisa de uma rolagem configurada.");
        var attribute = Attribute(system, key, paranormal);
        var severity = D20Conditions.Severity(s, attribute, key, attack, ranged, paranormal);
        if (!paranormal)
        {
            // Fear and blindness affect skills; weakness also affects raw attribute checks.
            if (!attack && T20Attributes.Contains(key))
            {
                var physical = key is "forca" or "destreza" or "constituicao";
                severity = physical ? D20Conditions.Has(s, "debilitado") ? 2 : D20Conditions.Has(s, "fraco") ? 1 : 0
                    : D20Conditions.Has(s, "esmorecido") ? 2 : D20Conditions.Has(s, "frustrado") ? 1 : 0;
            }
            var penalty = severity == 2 ? 5 : severity == 1 ? 2 : 0;
            var resolved = WeaponActions.Resolve(field!.RollExpression ?? $"1d20+{{{key}}}", s, false);
            RuleState.Require(DiceExpression.TryParse(resolved, out var dice) && dice is not null && dice.Count == 1 && dice.Sides == 20, "O teste desta edição exige 1d20.");
            // Carrying penalties are armor penalties and apply to the listed physical skills.
            if (D20Conditions.Has(s, "sobrecarregado") && (key is "per_acrobacia" or "per_furtividade" or "per_ladinagem" || key == "per_atletismo" && s.Get("atletismo_natacao") == 1)) penalty += 5;
            var modifier = dice!.Modifier - penalty;
            return "1d20" + (modifier >= 0 ? "+" : "") + modifier;
        }
        var burden = D20Conditions.Has(s, "sobrecarregado") && (key is "per_acrobacia" or "per_furtividade" or "per_crime");
        burden |= D20Conditions.Has(s, "sobrecarregado") && key == "per_atletismo" && s.Get("atletismo_natacao") == 1;
        if (severity == 0 && !burden) return WeaponActions.Resolve(field!.RollExpression!, s, true);
        RuleState.Require(OpAttribute().IsMatch(field!.RollExpression!), "A penalidade em dados exige um atributo explícito na rolagem do schema.");
        RuleState.Require(DiceExpression.TryParse(WeaponActions.Resolve(field.RollExpression!, s, true), out var opDice) && opDice is not null, "Expressão inválida.");
        return ParanormalExpression(s, attribute, opDice!.Modifier - (burden ? 5 : 0), severity);
    }
    public static string ParanormalExpression(RuleState s, string attribute, int bonus, int? severity = null, string key = "")
    {
        var penalty = severity ?? D20Conditions.Severity(s, attribute, key, false, false, true);
        var value = s.N(attribute);
        RuleState.Require(value is >= 0 and <= 20, "Atributo fora do intervalo suportado nesta edição.");
        var count = value == 0 ? 2 : value; var worst = value == 0;
        if (penalty > 0)
        {
            if (value == 0)
            {
                // The core text does not disambiguate combining zero's special pool and penalties.
                // Do not silently import the negative-attribute rule from a different supplement.
                count = s.Get("dados_zero_penalidade");
                RuleState.Require(count is >= 1 and <= 40, "Atributo zero com penalidade: configure a quantidade de dados decidida pela mesa; o menor será conservado.");
                worst = true;
            }
            else if (value - penalty >= 1) count = value - penalty;
            else { count = value + penalty; worst = true; }
        }
        return $"{count}d20{(count == 1 ? "" : worst ? "kl1" : "kh1")}{(bonus >= 0 ? "+" : "")}{bonus}";
    }
    public static bool Passed(RuleState s, int total, int target, bool paranormal)
    {
        var roll = s.Rolls[^1]; var natural = roll.Rolls[roll.KeptIndexes[0]];
        return natural == 20 || (paranormal || natural != 1) && total >= target;
    }
    public static string Test(SystemDefinition system, RuleState s, RuleCommand command)
    {
        var key = command.Option!;
        if (key == "per_reflexos" && D20Conditions.Has(s, "indefeso")) return "Reflexos: falha automática por indefeso; nenhum dado foi rolado.";
        if (key == "per_percepcao" && (s.Get("sentido_percepcao") == 1 && D20Conditions.Has(s, "cego") || s.Get("sentido_percepcao") == 2 && D20Conditions.Has(s, "surdo"))) return "Percepção: sentido indisponível; nenhum dado foi rolado.";
        var expression = Expression(system, s, key); var total = s.Roll(expression);
        return $"{system.Fields.Single(f => f.Key == key).Label}: {expression} [{string.Join(", ", s.Rolls[^1].Rolls)}] = {total}" + (command.Amount == 0 ? "." : $" / {command.Amount}: {(Passed(s, total, command.Amount, system.TemplateId == "ordem_paranormal") ? "sucesso" : "falha")}.");
    }
}
