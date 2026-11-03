using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronicles.Api;

public sealed record OccupationAllocation(string Key, int Base, int Occupation, int Interest);
public sealed record OccupationChoice(string Slot, string Skill);
public sealed record OccupationSkillView(string Key, string Label, int Base, int Occupation, int Interest, bool Occupational);
public sealed record OccupationView(int Phase, CthulhuOccupation[] Occupations, string Selected, CthulhuOccupationSlot[] Slots,
    OccupationChoice[] Choices, OccupationSkillView[] Skills, int OccupationalTotal, int InterestTotal, int OccupationalSpent, int InterestSpent, int CreditMin, int CreditMax, bool PersonalCredit, string? Error);

public static class CthulhuOccupationRules
{
    public const string Ledger = "criacao_pericias";
    public const string ChoicesKey = "criacao_escolhas";
    private static bool Supported(SystemDefinition system) => system.TemplateId == "call_of_cthulhu_7e" && system.RulesetVersion == "7e" && system.TemplateVersion == 6 && system.CthulhuOccupations is not null;
    private static string Fingerprint(SystemDefinition system) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(system.CthulhuOccupations))));
    private static void Save(RuleState s, OccupationAllocation[] rows) => s.Fields[Ledger] = JsonSerializer.SerializeToElement(rows.Select(r => new { chave = r.Key, @base = r.Base, ocupacao = r.Occupation, interesse = r.Interest }));
    // Character tables use their own stable Portuguese column keys.
    private static OccupationAllocation[] ReadAllocations(RuleState s)
    {
        if (!s.Fields.TryGetValue(Ledger, out var value)) return [];
        RuleState.Require(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= 100, "Distribuição de perícias inválida.");
        return value.EnumerateArray().Select(row => new OccupationAllocation(row.GetProperty("chave").GetString()!, row.GetProperty("base").GetInt32(), row.GetProperty("ocupacao").GetInt32(), row.GetProperty("interesse").GetInt32())).ToArray();
    }
    private static OccupationChoice[] ReadChoices(RuleState s)
    {
        if (!s.Fields.TryGetValue(ChoicesKey, out var value)) return [];
        RuleState.Require(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= 8, "Escolhas ocupacionais inválidas.");
        return value.EnumerateArray().Select(row => new OccupationChoice(row.GetProperty("slot").GetString()!, row.GetProperty("pericia").GetString()!)).ToArray();
    }
    private static CthulhuOccupation Profile(SystemDefinition system, RuleState s) => system.CthulhuOccupations!.Occupations.SingleOrDefault(o => o.Id == s.Text("ocupacao_regra")) ?? throw new ArgumentException("Ocupação não encontrada na versão do sistema.");
    public static OccupationView? Describe(SystemDefinition system, RuleState s)
    {
        if (!Supported(system)) return null;
        var phase = s.Get("ocupacao_fase"); var rows = ReadAllocations(s); var choices = ReadChoices(s);
        var profile = system.CthulhuOccupations!.Occupations.SingleOrDefault(o => o.Id == s.Text("ocupacao_regra"));
        var selected = choices.Select(c => c.Skill).ToHashSet();
        var error = phase == 1 && s.Text("ocupacao_versao") != Fingerprint(system) ? "As regras do modelo mudaram. Reinicie esta distribuição para usar a versão atual, ou peça ao mestre para restaurar a revisão anterior do sistema." : null;
        return new(phase, system.CthulhuOccupations.Occupations, s.Text("ocupacao_regra"), profile?.Slots ?? [], choices,
            rows.Select(row => new OccupationSkillView(row.Key, system.Fields.FirstOrDefault(f => f.Key == row.Key)?.Label ?? row.Key, row.Base, row.Occupation, row.Interest, row.Key == "credito" || selected.Contains(row.Key))).ToArray(),
            s.Get("ocupacao_total"), s.Get("ocupacao_interesse_total"), rows.Sum(r => r.Occupation), rows.Sum(r => r.Interest), s.Get("ocupacao_credito_min"), s.Get("ocupacao_credito_max"), system.CthulhuOccupations.PersonalCredit, error);
    }
    public static RuleAction[] Actions(SystemDefinition system, RuleState s)
    {
        if (!Supported(system)) return [];
        if (s.Get("ocupacao_fase") == 0 && s.Get("criacao_fase") == 2) return [new("occupation-start", "Iniciar distribuição de ocupação e interesses", null, system.CthulhuOccupations!.Occupations.Select(o => new RuleChoice(o.Id, o.Name)).ToArray())];
        return s.Get("ocupacao_fase") == 1 ? [new("occupation-choice", "Escolher perícia ocupacional", Hidden: true), new("occupation-allocate", "Distribuir pontos", Hidden: true), new("occupation-finish", "Concluir ocupação e perícias"), new("occupation-reset", "Reiniciar distribuição (devolve pontos e valores base)")] : [];
    }
    public static bool Handles(string action) => action.StartsWith("occupation-", StringComparison.Ordinal);
    private static int Evaluate(string formula, RuleState s)
    {
        try { var result = SheetFormula.Evaluate(formula, key => s.N(key)); RuleState.Require(result is >= 0 and <= 9999, "A fórmula de criação produziu um valor fora dos limites."); return result; }
        catch (Exception e) when (e is FormatException or OverflowException or DivideByZeroException) { throw new ArgumentException("Confira as fórmulas de criação e os valores das características."); }
    }
    public static string Apply(SystemDefinition system, RuleState s, RuleCommand command)
    {
        RuleState.Require(Supported(system), "Atualize para o modelo de ocupações 7e antes de distribuir pontos.");
        if (command.Action == "occupation-start")
        {
            RuleState.Require(s.Get("criacao_fase") == 2 && s.Get("ocupacao_fase") == 0 && ReadAllocations(s).Length == 0, "A distribuição exige um novo investigador com características concluídas.");
            var rules = system.CthulhuOccupations!; var profile = rules.Occupations.Single(o => o.Id == command.Option);
            RuleState.Require(s.Fields.Where(pair => pair.Key.StartsWith("per_", StringComparison.Ordinal) || pair.Key == "credito").All(pair => pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetInt32(out var number) && number == 0), "Já há perícias ou Crédito preenchidos. Os valores existentes foram preservados; não reinicie um investigador já jogado.");
            var total = Evaluate(profile.Points, s); var interest = Evaluate("inteligencia * 2", s);
            var rows = rules.Skills.Select(skill => new OccupationAllocation(skill.Key, Evaluate(skill.Formula, s), 0, 0)).Append(new("credito", 0, 0, 0)).ToArray();
            foreach (var row in rows) s.Field(row.Key, row.Base);
            Save(s, rows); s.Fields[ChoicesKey] = JsonSerializer.SerializeToElement(Array.Empty<object>());
            s.Fields["ocupacao_regra"] = JsonSerializer.SerializeToElement(profile.Id); s.Fields["ocupacao_versao"] = JsonSerializer.SerializeToElement(Fingerprint(system)); s.Fields["ocupacao"] = JsonSerializer.SerializeToElement(profile.Name);
            s.Set("ocupacao_fase", 1); s.Set("ocupacao_total", total); s.Set("ocupacao_interesse_total", interest); s.Set("ocupacao_credito_min", profile.CreditMin); s.Set("ocupacao_credito_max", profile.CreditMax);
            return "Bases aplicadas e orçamentos separados. Escolha oito perícias distintas e distribua os pontos; Crédito deve respeitar a faixa da ocupação.";
        }
        RuleState.Require(s.Get("ocupacao_fase") == 1, "Esta distribuição não está aberta.");
        var allocations = ReadAllocations(s);
        if (command.Action == "occupation-reset")
        {
            foreach (var row in allocations) s.Field(row.Key, 0);
            s.Fields[Ledger] = JsonSerializer.SerializeToElement(Array.Empty<object>()); s.Fields[ChoicesKey] = JsonSerializer.SerializeToElement(Array.Empty<object>());
            s.Fields["ocupacao_regra"] = JsonSerializer.SerializeToElement(""); s.Fields["ocupacao_versao"] = JsonSerializer.SerializeToElement("");
            foreach (var key in s.State.Keys.Where(k => k.StartsWith("ocupacao_", StringComparison.Ordinal)).ToArray()) s.Set(key, 0);
            return "Distribuição reiniciada. Os pontos foram devolvidos e as perícias voltaram ao estado anterior à ocupação. As bases serão aplicadas novamente ao escolher o perfil.";
        }
        RuleState.Require(s.Text("ocupacao_versao") == Fingerprint(system), "O modelo de ocupações mudou. Reinicie a distribuição ou restaure a revisão anterior.");
        var selectedProfile = Profile(system, s); var choices = ReadChoices(s);
        if (command.Action == "occupation-choice")
        {
            var slot = selectedProfile.Slots.SingleOrDefault(slot => slot.Id == command.Option);
            RuleState.Require(slot is not null && command.Expression is { } && system.CthulhuOccupations!.Skills.Any(skill => skill.Key == command.Expression) && (slot.Skills is null || slot.Skills.Contains(command.Expression)), "Perícia indisponível para esta escolha.");
            RuleState.Require(!choices.Any(choice => choice.Slot != slot!.Id && choice.Skill == command.Expression), "Escolha oito perícias diferentes.");
            var previous = choices.SingleOrDefault(choice => choice.Slot == slot!.Id);
            RuleState.Require(previous is null || previous.Skill == command.Expression || !allocations.Any(row => row.Key == previous.Skill && row.Occupation > 0), "Devolva os pontos profissionais da perícia antes de trocar esta escolha.");
            s.Fields[ChoicesKey] = JsonSerializer.SerializeToElement(choices.Where(choice => choice.Slot != slot!.Id).Append(new(slot!.Id, command.Expression!)).Select(c => new { slot = c.Slot, pericia = c.Skill }));
            return "Escolha ocupacional salva.";
        }
        if (command.Action == "occupation-allocate")
        {
            var key = command.Option!; var index = Array.FindIndex(allocations, row => row.Key == key);
            RuleState.Require(index >= 0 && command.Expression is "occupation" or "interest", "Perícia ou origem dos pontos inválida.");
            var occupational = command.Expression == "occupation";
            RuleState.Require(!occupational || key == "credito" || choices.Any(choice => choice.Skill == key), "A perícia não foi escolhida para a ocupação.");
            RuleState.Require(occupational || key != "credito" || system.CthulhuOccupations!.PersonalCredit, "Esta configuração usa apenas pontos profissionais para Crédito.");
            allocations[index] = occupational ? allocations[index] with { Occupation = command.Amount } : allocations[index] with { Interest = command.Amount };
            RuleState.Require(allocations.Sum(r => r.Occupation) <= s.Get("ocupacao_total") && allocations.Sum(r => r.Interest) <= s.Get("ocupacao_interesse_total"), "A distribuição ultrapassa o orçamento disponível.");
            var total = allocations[index].Base + allocations[index].Occupation + allocations[index].Interest;
            RuleState.Require(key != "credito" || total <= selectedProfile.CreditMax, "Crédito acima da faixa da ocupação.");
            RuleState.Require(key == "credito" || total <= (system.CthulhuOccupations!.SkillCap ?? 9999), "A perícia ultrapassa o limite de criação definido pela mesa.");
            Save(s, allocations); s.Field(key, total); return "Distribuição salva; o valor final inclui base, pontos profissionais e interesses pessoais.";
        }
        if (command.Action == "occupation-finish")
        {
            RuleState.Require(choices.Length == 8, "Escolha as oito perícias ocupacionais antes de concluir.");
            RuleState.Require(s.N("credito") >= selectedProfile.CreditMin && s.N("credito") <= selectedProfile.CreditMax, "Crédito fora da faixa da ocupação.");
            s.Set("ocupacao_fase", 2);
            return "Distribuição concluída. Pontos não utilizados foram descartados; o histórico conserva os valores de criação. Desenvolvimento posterior não altera esta distribuição inicial.";
        }
        throw new ArgumentException("Comando de ocupação desconhecido.");
    }
    public static string? Validate(SystemDefinition system, RuleState before, RuleState after, bool ruleExecution, bool newCharacter)
    {
        if (!Supported(system)) return null;
        try
        {
            var phase = after.Get("ocupacao_fase"); var rows = ReadAllocations(after); var choices = ReadChoices(after);
            if (phase is < 0 or > 2 || after.Get("ocupacao_total") is < 0 or > 9999 || after.Get("ocupacao_interesse_total") is < 0 or > 9999) return "Etapa ou orçamento de ocupação inválido.";
            if (rows.Select(r => r.Key).Distinct().Count() != rows.Length || rows.Any(r => string.IsNullOrWhiteSpace(r.Key) || r.Base is < 0 or > 9999 || r.Occupation is < 0 or > 9999 || r.Interest is < 0 or > 9999)
                || rows.Sum(r => r.Occupation) > after.Get("ocupacao_total") || rows.Sum(r => r.Interest) > after.Get("ocupacao_interesse_total")) return "Distribuição de pontos inválida ou acima do orçamento.";
            if (choices.Any(c => string.IsNullOrWhiteSpace(c.Slot) || string.IsNullOrWhiteSpace(c.Skill)) || choices.Select(c => c.Slot).Distinct().Count() != choices.Length || choices.Select(c => c.Skill).Distinct().Count() != choices.Length) return "Escolhas repetidas ou inválidas.";
            if (phase == 0 && (rows.Length > 0 || choices.Length > 0)) return "Distribuição sem etapa de criação aberta.";
            if (phase > 0 && (rows.Length == 0 || rows.All(r => r.Key != "credito"))) return "A distribuição precisa das bases e de Crédito.";
            if (phase == 2 && choices.Length != 8) return "Uma distribuição concluída exige oito escolhas.";
            if (!ruleExecution && !newCharacter)
            {
                bool Same(string key)
                {
                    var old = before.Fields.GetValueOrDefault(key); var next = after.Fields.GetValueOrDefault(key);
                    bool Empty(JsonElement value) => value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0 || value.ValueKind == JsonValueKind.String && value.GetString() == "";
                    return Empty(old) && Empty(next) || old.ValueKind != JsonValueKind.Undefined && next.ValueKind != JsonValueKind.Undefined && JsonElement.DeepEquals(old, next);
                }
                if (before.State.Keys.Concat(after.State.Keys).Where(k => k.StartsWith("ocupacao_", StringComparison.Ordinal)).Distinct().Any(k => before.Get(k) != after.Get(k))
                    || new[] { Ledger, ChoicesKey, "ocupacao_regra", "ocupacao_versao" }.Any(k => !Same(k))) return "Use os comandos de ocupação para alterar a distribuição ou sua etapa; importação/restauração não pode alterar esses controles de uma ficha existente.";
            }
            if (phase == 1)
            {
                if (rows.Any(r => after.N(r.Key) != r.Base + r.Occupation + r.Interest)) return "Use a distribuição de pontos para alterar as perícias enquanto a criação estiver aberta.";
                if (after.Text("ocupacao_versao") == Fingerprint(system))
                {
                    var rules = system.CthulhuOccupations!; var profile = Profile(system, after); var expected = rules.Skills.Select(s => s.Key).Append("credito").Order().ToArray();
                    if (!rows.Select(r => r.Key).Order().SequenceEqual(expected)) return "A lista de perícias não corresponde ao modelo.";
                    if (choices.Any(c => !rules.Skills.Any(s => s.Key == c.Skill) || !profile.Slots.Any(slot => slot.Id == c.Slot && (slot.Skills is null || slot.Skills.Contains(c.Skill))))) return "Escolha ocupacional incompatível com o modelo.";
                    if (after.N("credito") > profile.CreditMax) return "Crédito acima da faixa da ocupação.";
                    if (rows.Any(r => r.Occupation > 0 && r.Key != "credito" && !choices.Any(c => c.Skill == r.Key)
                        || r.Key == "credito" && !rules.PersonalCredit && r.Interest > 0 || r.Key != "credito" && r.Base + r.Occupation + r.Interest > (rules.SkillCap ?? 9999))) return "Pontos atribuídos a uma perícia não autorizada ou acima do limite da mesa.";
                }
            }
            return null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException) { return "Dados da distribuição de ocupação inválidos. Confira a revisão e as tabelas da ficha."; }
    }
}
