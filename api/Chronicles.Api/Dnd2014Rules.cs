namespace Chronicles.Api;

public static class Dnd2014Rules
{
    private static readonly (string Key, string Label, int Die, int Casting)[] Classes = [
        ("barbaro", "Bárbaro", 12, 0), ("bardo", "Bardo", 8, 1), ("bruxo", "Bruxo", 8, 0),
        ("clerigo", "Clérigo", 8, 1), ("druida", "Druida", 8, 1), ("feiticeiro", "Feiticeiro", 6, 1),
        ("guerreiro", "Guerreiro", 10, 3), ("ladino", "Ladino", 8, 3), ("mago", "Mago", 6, 1),
        ("monge", "Monge", 8, 0), ("paladino", "Paladino", 10, 2), ("patrulheiro", "Patrulheiro", 10, 2)
    ];
    private static readonly int[][] Slots = [[], [2], [3], [4,2], [4,3], [4,3,2], [4,3,3], [4,3,3,1], [4,3,3,2],
        [4,3,3,3,1], [4,3,3,3,2], [4,3,3,3,2,1], [4,3,3,3,2,1], [4,3,3,3,2,1,1], [4,3,3,3,2,1,1],
        [4,3,3,3,2,1,1,1], [4,3,3,3,2,1,1,1], [4,3,3,3,2,1,1,1,1], [4,3,3,3,3,1,1,1,1], [4,3,3,3,3,2,1,1,1], [4,3,3,3,3,2,2,1,1]];
    private static RuleChoice[] Dice => new[] { 6, 8, 10, 12 }.Select(d => new RuleChoice(d.ToString(), $"d{d}")).ToArray();
    private static int Level(RuleState s) => Classes.Sum(c => s.Get("nivel_" + c.Key));
    private static int HitDice(RuleState s, int die) => Classes.Where(c => c.Die == die).Sum(c => s.Get("nivel_" + c.Key));
    public static void SyncSlots(RuleState s)
    {
        var slots = SpellSlots(s);
        for (var i = 1; i <= 9; i++) s.Field("espacos_magia_" + i, Math.Max(0, (i <= slots.Length ? slots[i - 1] : 0) - s.Get("slot_usado_" + i)));
    }
    public static int[] SpellSlots(RuleState s)
    {
        var active = Classes.Where(c => s.Get("nivel_" + c.Key) > 0 && (c.Casting == 1 || c.Casting == 2 && s.Get("nivel_" + c.Key) >= 2 || c.Casting == 3 && s.Get("terco_" + c.Key) == 1 && s.Get("nivel_" + c.Key) >= 3)).ToArray();
        var single = active.Length == 1;
        var effective = active.Sum(c => c.Casting switch
        {
            1 => s.Get("nivel_" + c.Key),
            2 => s.Get("nivel_" + c.Key) < 2 ? 0 : single ? (s.Get("nivel_" + c.Key) + 1) / 2 : s.Get("nivel_" + c.Key) / 2,
            3 => s.Get("terco_" + c.Key) == 1 && s.Get("nivel_" + c.Key) >= 3 ? single ? (s.Get("nivel_" + c.Key) + 2) / 3 : s.Get("nivel_" + c.Key) / 3 : 0,
            _ => 0
        });
        return Slots[Math.Clamp(effective, 0, 20)];
    }
    public static RuleView Describe(RuleState s)
    {
        var slots = SpellSlots(s);
        var stats = new List<RuleStat> { new("Níveis configurados", Level(s).ToString()), new("Carga normal (libras)", (s.N("forca") * 15).ToString()),
            new("Estado", s.Get("morto") > 0 ? "Morto" : s.Health == 0 ? s.Get("estavel") > 0 ? "Estável / inconsciente" : "Inconsciente / testes contra a morte" : "Ativo") };
        stats.AddRange(Dice.Select(d => { var die = int.Parse(d.Value); return new RuleStat($"Dados de vida d{die}", $"{Math.Max(0, HitDice(s, die) - s.Get("dv_usado_" + die))}/{HitDice(s, die)}"); }));
        stats.AddRange(slots.Select((total, i) => new RuleStat($"Espaços de nível {i + 1}", $"{s.N("espacos_magia_" + (i + 1), Math.Max(0, total - s.Get("slot_usado_" + (i + 1))))}/{total}")));
        var warlock = s.Get("nivel_bruxo");
        if (warlock > 0) stats.Add(new("Magia de pacto", $"{Math.Max(0, PactCount(warlock) - s.Get("pacto_usado"))}/{PactCount(warlock)} de nível {Math.Min(5, (warlock + 1) / 2)}"));
        return new("D&D 5e 2014", [new("damage", "Sofrer dano", "Dano após resistências", [new("normal", "Normal"), new("critical", "Acerto crítico")]),
            new("heal", "Receber cura", "PV"), new("temporary", "Receber PV temporários", "PV temporários"),
            new("death-save", "Rolar teste contra a morte"), new("stabilize", "Estabilizar (efeito confirmado)"),
            new("hit-die", "Gastar dado de vida no descanso curto", null, Dice),
            new("long-rest", "Concluir descanso longo", null, [new("confirmed", "8h concluídas; nenhum descanso longo nas últimas 24h"), new("fed", "8h, alimentação e água; nenhum descanso longo nas últimas 24h")]),
            new("recover-hit-die", "Recuperar dado de vida", null, Dice), new("cast", "Gastar espaço de magia", "Nível do espaço (1–9)"),
            new("pact-cast", "Gastar espaço de pacto"), new("short-rest", "Concluir descanso curto (1h)"),
            new("spellcasting", "Usar conjuração de uma classe", null, Classes.Where(c => c.Casting > 0 || c.Key == "bruxo").Select(c => new RuleChoice(c.Key, c.Label)).ToArray()),
            new("baseline", "Aplicar PV médios de criação", null, Classes.Select(c => new RuleChoice(c.Key, $"Classe inicial: {c.Label}")).ToArray())], stats.ToArray(),
            [.. Classes.Select(c => new RuleSetting("nivel_" + c.Key, $"Níveis de {c.Label}", 0, 20)), new("terco_guerreiro", "Guerreiro com conjuração de 1/3 (0 ou 1)", 0, 1), new("terco_ladino", "Ladino com conjuração de 1/3 (0 ou 1)", 0, 1)], s.State);
    }
    private static int PactCount(int level) => level >= 17 ? 4 : level >= 11 ? 3 : level >= 2 ? 2 : 1;
    private static void Alive(RuleState s) => RuleState.Require(s.Get("morto") == 0, "Morte exige um efeito específico; cura e descanso comuns não revivem.");
    public static string Apply(RuleState s, RuleCommand c)
    {
        Alive(s);
        switch (c.Action)
        {
            case "damage":
                var absorbed = Math.Min(s.N("pontos_temporarios"), c.Amount);
                s.Field("pontos_temporarios", s.N("pontos_temporarios") - absorbed);
                var damage = c.Amount - absorbed;
                if (s.Health == 0 && c.Amount > 0) { s.Field("salvaguardas_falha", Math.Min(3, s.N("salvaguardas_falha") + (c.Option == "critical" ? 2 : 1))); s.Set("estavel", 0); }
                if (damage - s.Health >= s.MaxHealth) s.Set("morto", 1);
                s.Health -= damage;
                if (s.N("salvaguardas_falha") >= 3) s.Set("morto", 1);
                return "Dano aplicado; PV temporários absorvidos primeiro." + Dnd2014Conditions.CheckConcentration(s, c.Amount);
            case "heal": s.Health += c.Amount; if (s.Health > 0) { s.Field("salvaguardas_sucesso", 0); s.Field("salvaguardas_falha", 0); s.Set("estavel", 0); } return "Cura aplicada.";
            case "temporary": s.Field("pontos_temporarios", Math.Max(s.N("pontos_temporarios"), c.Amount)); return "PV temporários não acumulam; o maior valor foi mantido.";
            case "death-save":
                RuleState.Require(s.Health == 0 && s.Get("estavel") == 0, "O teste exige PV zero e personagem não estabilizado.");
                var die = s.Die(20);
                if (die == 20) { s.Health = 1; s.Field("salvaguardas_sucesso", 0); s.Field("salvaguardas_falha", 0); }
                else if (die >= 10) s.Field("salvaguardas_sucesso", Math.Min(3, s.N("salvaguardas_sucesso") + 1));
                else s.Field("salvaguardas_falha", Math.Min(3, s.N("salvaguardas_falha") + (die == 1 ? 2 : 1)));
                if (s.N("salvaguardas_falha") >= 3) s.Set("morto", 1);
                if (s.N("salvaguardas_sucesso") >= 3) Stabilize(s);
                return $"Teste contra a morte: {die}.";
            case "stabilize": RuleState.Require(s.Health == 0, "Estabilizar exige PV zero."); Stabilize(s); return "Estabilizado; sucessos e falhas reiniciados.";
            case "hit-die":
                var sides = int.Parse(c.Option!);
                RuleState.Require(s.Health > 0 && HitDice(s, sides) > s.Get("dv_usado_" + sides), "Sem dados de vida disponíveis ou personagem inconsciente.");
                s.Set("dv_usado_" + sides, s.Get("dv_usado_" + sides) + 1);
                var healed = Math.Max(0, s.Die(sides) + s.N("mod_constituicao")); s.Health += healed; return $"Dado gasto: até {healed} PV recuperados.";
            case "long-rest":
                RuleState.Require(s.Health > 0 && Level(s) is >= 1 and <= 20, "O descanso longo exige ao menos 1 PV no início e níveis configurados.");
                if (c.Option == "fed" && s.Get("exaustao") > 0) Dnd2014Conditions.Apply(new([], null), s, new("exhaustion", s.Get("exaustao") - 1));
                s.Health = s.MaxHealth; s.Field("pontos_temporarios", 0);
                for (var i = 1; i <= 9; i++) s.Set("slot_usado_" + i, 0);
                SyncSlots(s);
                s.Set("pacto_usado", 0); s.Set("dv_recuperar", Math.Max(1, Level(s) / 2));
                return "PV e espaços restaurados; escolha os dados de vida a recuperar (até metade dos níveis, mínimo 1).";
            case "recover-hit-die":
                var recoverDie = int.Parse(c.Option!);
                RuleState.Require(s.Get("dv_recuperar") > 0 && s.Get("dv_usado_" + recoverDie) > 0, "Não há dado gasto ou recuperação disponível.");
                s.Set("dv_recuperar", s.Get("dv_recuperar") - 1); s.Set("dv_usado_" + recoverDie, s.Get("dv_usado_" + recoverDie) - 1); return "Dado de vida recuperado.";
            case "cast":
                RuleState.Require(c.Amount is >= 1 and <= 9, "Nível do espaço inválido.");
                var slots = SpellSlots(s); var total = c.Amount <= slots.Length ? slots[c.Amount - 1] : 0;
                var current = s.N("espacos_magia_" + c.Amount, total - s.Get("slot_usado_" + c.Amount));
                RuleState.Require(current > 0 && current <= total, "Não há espaço deste nível disponível. Confira os níveis e os espaços registrados.");
                s.Set("slot_usado_" + c.Amount, total - current + 1); s.Field("espacos_magia_" + c.Amount, current - 1); return "Espaço gasto. Componentes e efeitos dependem da magia escolhida.";
            case "spellcasting":
                RuleState.Require(s.Get("nivel_" + c.Option) > 0, "Configure níveis da classe conjuradora.");
                RuleState.Require(c.Option is not ("paladino" or "patrulheiro") || s.Get("nivel_" + c.Option) >= 2, "Esta classe recebe conjuração a partir do nível 2.");
                RuleState.Require(c.Option is not ("guerreiro" or "ladino") || s.Get("nivel_" + c.Option) >= 3, "A subclasse recebe conjuração a partir do nível 3.");
                var attribute = c.Option is "clerigo" or "druida" or "patrulheiro" ? "sabedoria" : c.Option is "mago" or "guerreiro" or "ladino" ? "inteligencia" : "carisma";
                RuleState.Require(c.Option is not ("guerreiro" or "ladino") || s.Get("terco_" + c.Option) == 1, "A subclasse precisa conceder conjuração.");
                var attack = s.N("mod_" + attribute) + s.N("proficiencia");
                s.Field("ataque_magico", attack); s.Field("cd_magias", 8 + attack); s.Fields["atributo_conjuracao"] = System.Text.Json.JsonSerializer.SerializeToElement(attribute);
                return "Atributo, ataque e CD atualizados para a classe selecionada. Multiclasse conserva atributos de conjuração distintos.";
            case "pact-cast": RuleState.Require(s.Get("nivel_bruxo") > 0 && s.Get("pacto_usado") < PactCount(s.Get("nivel_bruxo")), "Sem espaço de pacto."); s.Set("pacto_usado", s.Get("pacto_usado") + 1); return "Espaço de pacto gasto.";
            case "short-rest": s.Set("pacto_usado", 0); return "Espaços de pacto restaurados. Gaste dados de vida individualmente para recuperar PV.";
            case "baseline":
                RuleState.Require(Level(s) is >= 1 and <= 20, "Configure de 1 a 20 níveis nas classes.");
                var first = Classes.Single(x => x.Key == c.Option);
                RuleState.Require(s.Get("nivel_" + first.Key) > 0, "A classe inicial precisa ter ao menos um nível.");
                var con = s.N("mod_constituicao");
                var hp = Math.Max(1, first.Die + con) + Classes.Sum(x => Math.Max(0, s.Get("nivel_" + x.Key) - (x.Key == first.Key ? 1 : 0)) * Math.Max(1, x.Die / 2 + 1 + con));
                s.HealthMaximum(hp); s.Field("nivel", Level(s)); return "PV médios básicos aplicados; bônus de raça, talentos e outras exceções devem ser ajustados.";
            default: throw new ArgumentException("Ação desconhecida.");
        }
    }
    private static void Stabilize(RuleState s) { s.Set("estavel", 1); s.Field("salvaguardas_sucesso", 0); s.Field("salvaguardas_falha", 0); }
}
