using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chronicles.Api;

// Small arithmetic language for sheet authors. No script execution or dynamic code.
public static class SheetFormula
{
    private static readonly Regex Tokens = new(@"\G\s*([a-z][a-z0-9_]*|[0-9]+|[()+*/-])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? Validate(SystemField[] fields)
    {
        var byKey = fields.ToDictionary(x => x.Key, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field.RollExpression is { Length: > 0 } roll && (field.Type != "number" || !ValidRoll(roll, byKey)))
                return $"Rolagem inválida em {field.Label}. Use, por exemplo, 1d20+3.";
            if (string.IsNullOrWhiteSpace(field.Formula)) continue;
            if (field.Type != "number" || field.Formula.Length > 200) return $"Fórmula inválida em {field.Label}.";
            try { Evaluate(field.Formula, key => byKey.TryGetValue(key, out var source) && source.Type == "number" ? 1 : throw new FormatException(), syntaxOnly: true); }
            catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException) { return $"Fórmula inválida em {field.Label}: use chaves de campos numéricos e +, -, *, / ou parênteses."; }
        }
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string key)
        {
            if (visited.Contains(key)) return true;
            if (!visiting.Add(key)) return false;
            foreach (var dependency in References(byKey[key].Formula))
                if (byKey[dependency].Formula is { Length: > 0 } && !Visit(dependency)) return false;
            visiting.Remove(key); visited.Add(key); return true;
        }
        return fields.Where(x => !string.IsNullOrWhiteSpace(x.Formula)).All(x => Visit(x.Key)) ? null : "As fórmulas não podem depender umas das outras em ciclo.";
    }

    public static string? Apply(SystemField[] fields, Dictionary<string, JsonElement> values)
    {
        var calculated = fields.Where(x => !string.IsNullOrWhiteSpace(x.Formula)).ToDictionary(x => x.Key, StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        int Resolve(string key)
        {
            if (calculated.TryGetValue(key, out var field))
            {
                if (done.Contains(key)) return values[key].GetInt32();
                if (!visiting.Add(key)) throw new FormatException();
                var result = Evaluate(field.Formula!, Resolve);
                if (field.Min is { } min && result < min || field.Max is { } max && result > max) throw new OverflowException();
                values[key] = JsonSerializer.SerializeToElement(result);
                visiting.Remove(key); done.Add(key);
                return result;
            }
            return values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
        }
        try { foreach (var key in calculated.Keys) Resolve(key); }
        catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException) { return "Não foi possível calcular a ficha. Confira valores, divisões e limites das fórmulas."; }
        return null;
    }

    private static IEnumerable<string> References(string? expression) =>
        expression is null ? [] : Regex.Matches(expression, @"[a-z][a-z0-9_]*").Select(x => x.Value);

    private static bool ValidRoll(string expression, Dictionary<string, SystemField> fields)
    {
        if (expression.Length > 80) return false;
        var placeholders = Regex.Matches(expression, @"\{([a-z][a-z0-9_]*)\}");
        foreach (Match match in placeholders)
            if (!fields.TryGetValue(match.Groups[1].Value, out var field) || field.Type != "number") return false;
        var sample = Regex.Replace(expression, @"\{[a-z][a-z0-9_]*\}", "1");
        return DiceExpression.TryParse(sample, out _);
    }

    public static int Evaluate(string expression, Func<string, int> resolve, bool syntaxOnly = false)
    {
        var tokens = new List<string>(); var offset = 0;
        while (offset < expression.Length)
        {
            var match = Tokens.Match(expression, offset);
            if (!match.Success) { if (string.IsNullOrWhiteSpace(expression[offset..])) break; throw new FormatException(); }
            tokens.Add(match.Groups[1].Value); offset = match.Index + match.Length;
        }
        if (tokens.Count == 0) throw new FormatException();
        var index = 0;
        int Atom()
        {
            if (index >= tokens.Count) throw new FormatException();
            var token = tokens[index++];
            if (token == "-") { var operand = Atom(); return syntaxOnly ? 1 : checked(-operand); }
            if (token == "+") return Atom();
            if (token == "(") { var nested = Sum(); if (index >= tokens.Count || tokens[index++] != ")") throw new FormatException(); return nested; }
            if (int.TryParse(token, out var constant)) return syntaxOnly ? 1 : constant;
            if (Regex.IsMatch(token, @"^[a-z][a-z0-9_]*$")) return resolve(token);
            throw new FormatException();
        }
        int Product()
        {
            var result = Atom();
            while (index < tokens.Count && tokens[index] is "*" or "/")
            {
                var operation = tokens[index++]; var right = Atom();
                result = syntaxOnly ? 1 : operation == "*" ? checked(result * right) : checked(result / right);
            }
            return result;
        }
        int Sum()
        {
            var result = Product();
            while (index < tokens.Count && tokens[index] is "+" or "-")
            {
                var operation = tokens[index++]; var right = Product();
                result = syntaxOnly ? 1 : operation == "+" ? checked(result + right) : checked(result - right);
            }
            return result;
        }
        var value = Sum();
        if (index != tokens.Count) throw new FormatException();
        return value;
    }
}
