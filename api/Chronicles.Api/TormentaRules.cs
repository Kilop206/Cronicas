namespace Chronicles.Api;

public static class TormentaRules
{
    private static readonly (string Key, string Label, int Initial, int Step, int Mana)[] Classes = [
        ("arcanista", "Arcanista", 8, 2, 6), ("barbaro", "Bárbaro", 24, 6, 3), ("bardo", "Bardo", 12, 3, 4),
        ("bucaneiro", "Bucaneiro", 16, 4, 3), ("cacador", "Caçador", 16, 4, 4), ("cavaleiro", "Cavaleiro", 20, 5, 3),
        ("clerigo", "Clérigo", 16, 4, 5), ("druida", "Druida", 16, 4, 4), ("guerreiro", "Guerreiro", 20, 5, 3),
        ("inventor", "Inventor", 12, 3, 4), ("ladino", "Ladino", 12, 3, 4), ("lutador", "Lutador", 20, 5, 3),
        ("nobre", "Nobre", 16, 4, 4), ("paladino", "Paladino", 20, 5, 3)
    ];
    private static int Level(RuleState s) => Classes.Sum(c => s.Get("nivel_" + c.Key));
    private static int Current(RuleState s) => s.Health - s.Get("pv_negativos");
    private static void Health(RuleState s, int health) { s.Health = Math.Max(0, health); s.Set("pv_negativos", Math.Max(0, -health)); }
    public static RuleView Describe(RuleState s) => new("Tormenta20 Jogo do Ano v1.3", [
        new("damage", "Sofrer dano", "Dano após resistências"), new("heal", "Receber cura", "PV"),
        new("spend", "Gastar PM em habilidade", "Custo", Classes.Select(c => new RuleChoice(c.Key, c.Label)).ToArray()),
        new("spend-general", "Gastar PM: raça, origem ou poder geral", "Custo"), new("spend-minimum", "Usar habilidade em seu custo mínimo", "Custo mínimo"),
        new("cast", "Conjurar magia conhecida", "Custo em PM", [.. Classes.Select(c => new RuleChoice(c.Key, c.Label)), new("general", "Raça, origem, poder ou item")]),
        new("cast-minimum", "Conjurar magia conhecida em seu custo mínimo", "Custo mínimo em PM"),
        new("scene", "Iniciar nova cena"),
        new("sleep", "Concluir noite de descanso (8h)", null, RuleState.RestOptions), new("turn", "Iniciar turno e resolver sangramento"), new("stabilize", "Cura CD 15 bem-sucedida"),
        new("baseline", "Aplicar PV e PM básicos de criação", null, Classes.Select(c => new RuleChoice(c.Key, $"Classe inicial: {c.Label}")).ToArray())
    ], [new("Níveis configurados", Level(s).ToString()), new("PV efetivos", Current(s).ToString()),
        new("Estado", s.Get("morto") > 0 ? "Morto" : Current(s) <= 0 ? s.Get("sangrando") > 0 ? "Inconsciente / sangrando" : "Inconsciente / estável" : "Ativo"),
        new("Carga sem penalidade (espaços)", (10 + s.N("forca") * 2 + s.Get("carga_bonus")).ToString()),
        new("Limite de morte (PV)", (-Math.Max(10, s.MaxHealth / 2)).ToString())],
        [.. Classes.Select(c => new RuleSetting("nivel_" + c.Key, $"Níveis de {c.Label}", 0, 20)),
            new("pm_bonus", "Ajuste de PM: atributo-chave e demais efeitos", -100, 100), new("pv_bonus", "Ajuste de PV: raça, poderes e demais efeitos", -100, 100), new("carga_bonus", "Espaços adicionais de carga", 0, 100), new("resistencia_fogo", "Redução aplicável ao dano de fogo", 0, 9999),
            new("condicao_conjuracao", "Conjuração: 0 normal, 1 ruim, 2 terrível", 0, 2), new("dano_durante_conjuracao", "Dano sofrido durante a execução da próxima magia", 0, 9999),
            new("magia_arcana_armadura", "Magia atual exige teste por armadura arcana (0 ou 1)", 0, 1), new("penalidade_armadura_magia", "Penalidade de armadura para Misticismo (valor positivo)", 0, 50)], s.State);
    public static string Apply(SystemDefinition system, RuleState s, RuleCommand c)
    {
        RuleState.Require(s.Get("morto") == 0, "Personagens mortos precisam de um efeito específico.");
        switch (c.Action)
        {
            case "damage": Health(s, Current(s) - c.Amount); if (Current(s) <= 0 && c.Amount > 0) s.Set("sangrando", 1); Death(s); return "Dano aplicado. PV negativos são preservados na ficha; a barra principal permanece em zero.";
            case "heal": Health(s, Current(s) + c.Amount); if (c.Amount > 0) s.Set("sangrando", 0); return "Cura aplicada aos PV efetivos, incluindo o saldo negativo.";
            case "stabilize": s.Set("sangrando", 0); return "Estabilização confirmada.";
            case "turn":
                var turnMessage = D20Conditions.StartTurn(s);
                if (D20Conditions.Has(s, "em_chamas")) { Health(s, Current(s) - D20Conditions.FireDamage(s, false)); Death(s); turnMessage += "Chamas resolvidas com a redução configurada. "; }
                if (s.Get("sangrando") == 0) return turnMessage + "Turno iniciado; sem sangramento.";
                var penalty = D20Conditions.Severity(s, "constituicao", "constituicao", false, false, false);
                var bleedingCon = s.N("constituicao") - (penalty == 2 ? 5 : penalty == 1 ? 2 : 0); var test = s.Roll($"1d20{(bleedingCon < 0 ? bleedingCon.ToString() : "+" + bleedingCon)}");
                if (D20Checks.Passed(s, test, 15, false)) { s.Set("sangrando", 0); return turnMessage + $"Constituição {test}: sangramento encerrado."; }
                Health(s, Current(s) - s.Die(6)); Death(s); return turnMessage + $"Constituição {test}: sangramento causou perda de 1d6 PV.";
            case "spend":
            case "spend-general":
            case "spend-minimum":
                RuleState.Require(!D20Conditions.CannotAct(s, physical: false), "As condições atuais impedem usar habilidades.");
                var cost = c.Amount + D20Conditions.CostIncrease(s);
                var limit = c.Action == "spend" ? s.Get("nivel_" + c.Option) : s.N("nivel");
                RuleState.Require(cost <= limit || c.Action == "spend-minimum", "O custo ultrapassa o nível correspondente. Use a ação de custo mínimo somente para o custo mínimo da habilidade.");
                s.SpendMana(cost); return "Custo descontado dos PM, incluindo alquebrado quando aplicável.";
            case "cast":
            case "cast-minimum":
                RuleState.Require(!D20Conditions.CannotAct(s), "As condições atuais impedem conjurar magias.");
                var castCost = c.Amount + D20Conditions.CostIncrease(s);
                var castLimit = c.Option == "general" ? s.N("nivel") : s.Get("nivel_" + c.Option);
                RuleState.Require(c.Action == "cast-minimum" || castCost <= castLimit, "O custo da magia ultrapassa o nível correspondente.");
                s.SpendMana(castCost);
                var circumstance = Math.Max(s.Get("condicao_conjuracao"), D20Conditions.Has(s, "agarrado") ? 2 : D20Conditions.Has(s, "caido") || D20Conditions.Has(s, "surdo") ? 1 : 0);
                var damageTarget = s.Get("dano_durante_conjuracao"); s.Set("dano_durante_conjuracao", 0);
                foreach (var target in new[] { circumstance == 0 ? 0 : (circumstance == 2 ? 20 : 15) + castCost, damageTarget }.Where(t => t > 0))
                {
                    var result = s.Roll(D20Checks.Expression(system, s, "per_vontade"));
                    if (!D20Checks.Passed(s, result, target, false)) return $"Magia perdida: Vontade {result} / CD {target}; {castCost} PM gastos.";
                }
                if (s.Get("magia_arcana_armadura") > 0 && c.Option != "general")
                {
                    var expression = D20Checks.Expression(system, s, "per_misticismo");
                    DiceExpression.TryParse(expression, out var dice); var modifier = dice!.Modifier - s.Get("penalidade_armadura_magia");
                    var result = s.Roll("1d20" + (modifier >= 0 ? "+" : "") + modifier);
                    if (!D20Checks.Passed(s, result, 20 + castCost, false)) return $"Magia perdida pela armadura: Misticismo {result} / CD {20 + castCost}; {castCost} PM gastos.";
                }
                return $"Magia conhecida conjurada: {castCost} PM gastos. Aplique o efeito da magia escolhida.";
            case "scene": D20Conditions.EndScene(s); return "Condições da cena encerradas; condições persistentes e sangramento foram mantidos.";
            case "sleep":
                RuleState.Require(s.Health > 0, "O personagem precisa estar consciente para descansar normalmente.");
                var recovery = RuleState.Recovery(s.N("nivel", 1), c.Option);
                s.Health += recovery; s.Mana += recovery; return $"Até {recovery} PV e PM recuperados.";
            case "baseline":
                RuleState.Require(Level(s) is >= 1 and <= 20, "Configure de 1 a 20 níveis nas classes.");
                var first = Classes.Single(x => x.Key == c.Option);
                RuleState.Require(s.Get("nivel_" + first.Key) > 0, "A classe inicial precisa ter ao menos um nível.");
                var con = s.N("constituicao");
                var hp = Math.Max(1, first.Initial + con) + Classes.Sum(x => Math.Max(0, s.Get("nivel_" + x.Key) - (x.Key == first.Key ? 1 : 0)) * Math.Max(1, x.Step + con)) + s.Get("pv_bonus");
                var mana = Classes.Sum(x => s.Get("nivel_" + x.Key) * x.Mana) + s.Get("pm_bonus");
                s.Vitals(hp, mana); s.Field("nivel", Level(s)); s.Set("pv_negativos", 0); return "Recursos básicos de classe aplicados com os ajustes configurados.";
            default: throw new ArgumentException("Ação desconhecida.");
        }
    }
    private static void Death(RuleState s) { if (Current(s) <= -Math.Max(10, s.MaxHealth / 2)) s.Set("morto", 1); }
}
