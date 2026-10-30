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
        new("sleep", "Concluir noite de descanso (8h)", null, RuleState.RestOptions), new("turn", "Iniciar turno e resolver sangramento"), new("stabilize", "Cura CD 15 bem-sucedida"),
        new("baseline", "Aplicar PV e PM básicos de criação", null, Classes.Select(c => new RuleChoice(c.Key, $"Classe inicial: {c.Label}")).ToArray())
    ], [new("Níveis configurados", Level(s).ToString()), new("PV efetivos", Current(s).ToString()),
        new("Estado", s.Get("morto") > 0 ? "Morto" : Current(s) <= 0 ? s.Get("sangrando") > 0 ? "Inconsciente / sangrando" : "Inconsciente / estável" : "Ativo"),
        new("Carga sem penalidade (espaços)", (10 + s.N("forca") * 2 + s.Get("carga_bonus")).ToString()),
        new("Limite de morte (PV)", (-Math.Max(10, s.MaxHealth / 2)).ToString())],
        [.. Classes.Select(c => new RuleSetting("nivel_" + c.Key, $"Níveis de {c.Label}", 0, 20)),
            new("pm_bonus", "Ajuste de PM: atributo-chave e demais efeitos", -100, 100), new("pv_bonus", "Ajuste de PV: raça, poderes e demais efeitos", -100, 100), new("carga_bonus", "Espaços adicionais de carga", 0, 100)], s.State);
    public static string Apply(RuleState s, RuleCommand c)
    {
        RuleState.Require(s.Get("morto") == 0, "Personagens mortos precisam de um efeito específico.");
        switch (c.Action)
        {
            case "damage": Health(s, Current(s) - c.Amount); if (Current(s) <= 0 && c.Amount > 0) s.Set("sangrando", 1); Death(s); return "Dano aplicado. PV negativos são preservados na ficha; a barra principal permanece em zero.";
            case "heal": Health(s, Current(s) + c.Amount); if (c.Amount > 0) s.Set("sangrando", 0); return "Cura aplicada aos PV efetivos, incluindo o saldo negativo.";
            case "stabilize": s.Set("sangrando", 0); return "Estabilização confirmada.";
            case "turn":
                if (s.Get("sangrando") == 0) return "Turno iniciado; sem sangramento.";
                var bleedingCon = s.N("constituicao"); var test = s.Roll($"1d20{(bleedingCon < 0 ? bleedingCon.ToString() : "+" + bleedingCon)}");
                var natural = s.Rolls[^1].Rolls[0];
                if (natural == 20 || natural != 1 && test >= 15) { s.Set("sangrando", 0); return $"Constituição {test}: sangramento encerrado."; }
                Health(s, Current(s) - s.Die(6)); Death(s); return $"Constituição {test}: sangramento causou perda de 1d6 PV.";
            case "spend":
                RuleState.Require(c.Amount <= s.Get("nivel_" + c.Option), "O custo ultrapassa o nível na classe que fornece a habilidade. Aprimoramentos de limite exigem configuração própria.");
                s.SpendMana(c.Amount); return "Custo descontado dos PM.";
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
