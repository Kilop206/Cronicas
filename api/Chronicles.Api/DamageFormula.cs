using System.Text.RegularExpressions;

namespace Chronicles.Api;

// Bounded arithmetic of dice and constants. References resolve only to numeric sheet fields.
public static partial class DamageFormula
{
    [GeneratedRegex(@"\{([a-z][a-z0-9_]{0,39})\}", RegexOptions.CultureInvariant)] private static partial Regex Reference();
    [GeneratedRegex(@"([+-]?)(?:(\d{1,2})d(100|20|12|10|8|6|4|3)|(\d{1,4}))", RegexOptions.CultureInvariant)] private static partial Regex Term();
    public static (int Sign, int Count, int Sides, int Constant)[] Parse(string source, RuleState s)
    {
        RuleState.Require(source.Length is > 0 and <= 160, "Informe dano com até 160 caracteres.");
        var expression = Reference().Replace(source.ToLowerInvariant(), m =>
        {
            RuleState.Require(s.Fields.TryGetValue(m.Groups[1].Value, out var field) && field.ValueKind == System.Text.Json.JsonValueKind.Number, "Referência numérica de dano desconhecida.");
            return s.N(m.Groups[1].Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }).Replace(" ", "").Replace("+-", "-").Replace("--", "+");
        var matches = Term().Matches(expression);
        RuleState.Require(matches.Count is > 0 and <= 12 && string.Concat(matches.Select(m => m.Value)) == expression, "Use apenas dados, números, referências numéricas e sinais +/−.");
        RuleState.Require(matches.Skip(1).All(m => m.Groups[1].Value.Length == 1), "Separe os termos de dano com + ou −.");
        var terms = matches.Select(m => (Sign: m.Groups[1].Value == "-" ? -1 : 1,
            Count: m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0,
            Sides: m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0,
            Constant: m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0)).ToArray();
        RuleState.Require(terms.All(t => t.Sides == 0 || t.Count >= 1) && terms.Sum(t => t.Count) <= 40, "Dano aceita até 40 dados, com quantidade positiva.");
        return terms;
    }
    public static int Evaluate(string source, RuleState s, int multiplier = 1, bool maximum = false)
    {
        RuleState.Require(multiplier is >= 1 and <= 5, "Multiplicador inválido.");
        var terms = Parse(source, s);
        RuleState.Require(terms.Sum(t => t.Count) * multiplier <= 80, "O dano crítico excede 80 dados.");
        var result = 0;
        foreach (var term in terms)
        {
            if (term.Sides == 0) result += term.Sign * term.Constant;
            else if (maximum) result += term.Sign * term.Count * multiplier * term.Sides;
            else result += term.Sign * s.Pool(term.Count * multiplier, term.Sides);
        }
        return result;
    }
}
