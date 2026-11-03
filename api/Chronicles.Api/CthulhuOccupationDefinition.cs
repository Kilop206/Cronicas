using System.Text.RegularExpressions;

namespace Chronicles.Api;

public sealed record CthulhuSkillBase(string Key, string Formula);
public sealed record CthulhuOccupationSlot(string Id, string Label, string[]? Skills = null);
public sealed record CthulhuOccupation(string Id, string Name, string Points, int CreditMin, int CreditMax, CthulhuOccupationSlot[] Slots);
public sealed record CthulhuOccupationDefinition(CthulhuSkillBase[] Skills, CthulhuOccupation[] Occupations, int? SkillCap = null, bool PersonalCredit = false)
{
    public static string? Validate(CthulhuOccupationDefinition? rules, SystemField[] fields)
    {
        if (rules is null) return null;
        bool Table(string key, params (string Key, string Type)[] columns) => fields.Any(f => f.Key == key && f.Type == "table" && columns.All(c => f.Columns?.Any(column => column.Key == c.Key && column.Type == c.Type) == true));
        if (!Table(CthulhuOccupationRules.Ledger, ("chave", "text"), ("base", "number"), ("ocupacao", "number"), ("interesse", "number"))
            || !Table(CthulhuOccupationRules.ChoicesKey, ("slot", "text"), ("pericia", "text")) || !Table("estado_regras", ("chave", "text"), ("valor", "number"))
            || !new[] { "ocupacao_regra", "ocupacao_versao", "ocupacao" }.All(key => fields.Any(f => f.Key == key && f.Type == "text"))) return "Inclua os campos de criação do modelo antes de configurar ocupações.";
        if (rules.Skills is not {Length: > 0 and <= 60} || rules.Occupations is not {Length: > 0 and <= 30} || rules.SkillCap is < 1 or > 9999) return "Definição de ocupações fora dos limites.";
        bool Id(string? value) => value is not null && Regex.IsMatch(value, "^[a-z][a-z0-9_]{1,39}$", RegexOptions.CultureInvariant);
        bool Formula(string? value)
        {
            if (value is not {Length: > 0 and <= 200}) return false;
            var suffix = 0; var key = "formula_ocupacao_interna";
            while (fields.Any(f => f.Key == key)) key = "formula_ocupacao_" + ++suffix;
            return SheetFormula.Validate([.. fields, new(key, "Expressão", "number", "Criação", null, null, null, Formula: value)]) is null;
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in rules.Skills)
            if (skill is null || !Id(skill.Key) || !skill.Key.StartsWith("per_", StringComparison.Ordinal) || skill.Key == "per_mitos" || !keys.Add(skill.Key)
                || !fields.Any(f => f.Key == skill.Key && f.Type == "number" && string.IsNullOrWhiteSpace(f.Formula)) || !Formula(skill.Formula)) return "Perícia/base de criação inválida, repetida ou calculada.";
        if (!fields.Any(f => f.Key == "credito" && f.Type == "number" && string.IsNullOrWhiteSpace(f.Formula))) return "Defina Crédito como campo numérico editável.";
        if (rules.Occupations.Select(o => o?.Id).Distinct(StringComparer.Ordinal).Count() != rules.Occupations.Length) return "Ocupações repetidas.";
        foreach (var occupation in rules.Occupations)
        {
            if (occupation is null || !Id(occupation.Id) || occupation.Name?.Trim().Length is not (>= 2 and <= 80) || !Formula(occupation.Points)
                || occupation.CreditMin < 0 || occupation.CreditMax > 99 || occupation.CreditMin > occupation.CreditMax || occupation.Slots is not {Length: 8}) return "Defina ocupação, fórmula, faixa de Crédito e oito escolhas de perícias.";
            if (occupation.Slots.Any(slot => slot is null || !Id(slot.Id) || slot.Label?.Trim().Length is not (>= 2 and <= 80)
                || slot.Skills is {Length: 0 or > 60} || slot.Skills is not null && (slot.Skills.Any(k => !keys.Contains(k)) || slot.Skills.Distinct().Count() != slot.Skills.Length))
                || occupation.Slots.Select(slot => slot.Id).Distinct().Count() != 8) return "Escolhas ocupacionais inválidas ou repetidas.";
        }
        return null;
    }
}
