namespace Chronicles.Api;

public static class CthulhuRules
{
    public static string DamageBonus(int strength, int size)
    {
        var total = strength + size;
        return total < 2 ? "0" : total <= 64 ? "-2" : total <= 84 ? "-1" : total <= 124 ? "0" : total <= 164 ? "1d4" : total <= 204 ? "1d6" : $"{2 + (total - 205) / 80}d6";
    }
    public static int Movement(int strength, int dexterity, int size, int age) => Math.Max(0,
        (strength < size && dexterity < size ? 7 : strength > size && dexterity > size ? 9 : 8) - Math.Clamp(age / 10 - 3, 0, 5));
    private static int SanityCap(RuleState s) => Math.Max(0, 99 - s.Get("mitos"));
    public static RuleView Describe(RuleState s) => new("Call of Cthulhu 7e", [
        new("damage", "Sofrer dano", "Dano após proteção"), new("heal", "Receber cura", "PV"),
        new("sanity-loss", "Perder sanidade", "SAN perdida"), new("sanity-heal", "Recuperar sanidade", "SAN"),
        new("day", "Iniciar novo dia de investigação"), new("spend-magic", "Gastar pontos de magia", "Custo", [new("magic", "Somente PM"), new("health", "Pagar PM faltantes com PV")]),
        new("magic-recovery", "Recuperar PM por hora", "Horas completas"),
        new("natural-healing", "Recuperação natural", null, [new("day", "Um dia; sem ferimento grave"), new("week", "Uma semana; ferimento grave")]),
        new("dying-check", "Teste de CON: morrendo"), new("stabilize", "Primeiros socorros bem-sucedidos"),
        new("luck-spend", "Gastar sorte opcional", "Sorte (teste elegível confirmado)"),
        new("skill-mark", "Marcar perícia usada com sucesso", null, SkillOptions(s)),
        new("skill-develop", "Desenvolver perícia marcada", null, SkillOptions(s))
    ], [new("Movimento", Movement(s.N("forca"), s.N("destreza"), s.N("tamanho"), s.N("idade")).ToString()),
        new("Sanidade máxima por Mitos", SanityCap(s).ToString()), new("SAN perdida no dia", s.Get("san_perdida").ToString()),
        new("Estado físico", s.Get("morto") > 0 ? "Morto" : s.Get("morrendo") > 0 ? "Morrendo" : s.Get("ferimento_grave") > 0 ? "Ferimento grave" : s.Health == 0 ? "Inconsciente" : "Ativo"),
        new("Estado mental", s.Sanity == 0 ? "Insanidade permanente" : s.Get("insanidade_indefinida") > 0 ? "Insanidade indefinida" : s.Get("insanidade_temporaria") > 0 ? "Insanidade temporária" : "Estável")],
        [new("mitos", "Perícia Mitos de Cthulhu", 0, 99), new("sorte_opcional", "Permitir gasto opcional de sorte (0 ou 1)", 0, 1)], s.State);
    private static RuleChoice[] SkillOptions(RuleState s) => s.Fields.Keys.Where(k => k.StartsWith("per_", StringComparison.Ordinal) && k != "per_mitos").Select(k => new RuleChoice(k, k[4..].Replace('_', ' '))).ToArray();
    private static void Alive(RuleState s) => RuleState.Require(s.Get("morto") == 0, "Personagens mortos precisam de um efeito específico.");
    public static string Apply(RuleState s, RuleCommand c)
    {
        Alive(s);
        switch (c.Action)
        {
            case "damage":
                if (c.Amount > s.MaxHealth) s.Set("morto", 1);
                if (c.Amount * 2 >= s.MaxHealth && c.Amount > 0) s.Set("ferimento_grave", 1);
                s.Health -= c.Amount;
                if (s.Health == 0 && s.Get("ferimento_grave") > 0) s.Set("morrendo", 1);
                if (s.Get("morto") > 0) return "Dano maior que PV máximos em um golpe: morte imediata (Livro Básico 7e).";
                if (c.Amount * 2 >= s.MaxHealth && s.Health > 0)
                {
                    var con = s.Die(100); if (con > s.N("constituicao")) s.Set("inconsciente", 1);
                    return $"Ferimento grave. CON {con} / {s.N("constituicao")}: " + (con <= s.N("constituicao") ? "permanece consciente." : "inconsciente.");
                }
                return "Dano aplicado.";
            case "heal": s.Health += c.Amount; return "Cura aplicada. Ferimentos graves e estabilização são tratados separadamente.";
            case "sanity-loss":
                var before = s.Sanity;
                if (!s.State.ContainsKey("san_inicio")) s.Set("san_inicio", before);
                s.Sanity -= c.Amount;
                var loss = before - s.Sanity; s.Set("san_perdida", s.Get("san_perdida") + loss);
                if (s.Get("san_perdida") * 5 >= s.Get("san_inicio") && loss > 0) s.Set("insanidade_indefinida", 1);
                if (loss >= 5 && s.Die(100) <= s.N("inteligencia")) { s.Set("insanidade_temporaria", 1); return "Perda aplicada; teste de INT bem-sucedido: insanidade temporária. O Guardião define a crise e suas consequências."; }
                return "Perda de SAN aplicada; confira o estado mental e as consequências com o Guardião.";
            case "sanity-heal": s.Sanity = Math.Min(SanityCap(s), s.Sanity + c.Amount); return "SAN recuperada até o limite de 99 − Mitos.";
            case "day": s.Set("san_inicio", s.Sanity); s.Set("san_perdida", 0); return "Novo período diário de perda de SAN iniciado; insanidades não foram removidas.";
            case "spend-magic":
                RuleState.Require(c.Amount > 0, "Informe um custo positivo.");
                var missing = Math.Max(0, c.Amount - s.Mana);
                RuleState.Require(missing == 0 || c.Option == "health" && s.Health >= missing, "PM insuficientes. Confirme pagamento em PV ou reduza o custo.");
                s.Mana -= c.Amount; s.Health -= missing; return $"Custo aplicado: {c.Amount - missing} PM e {missing} PV.";
            case "magic-recovery": RuleState.Require(c.Amount is >= 1 and <= 168, "Informe de 1 a 168 horas completas."); s.Mana += c.Amount; return "Recuperação de 1 PM por hora aplicada.";
            case "natural-healing":
                RuleState.Require(s.Get("morrendo") == 0, "Estabilize antes da recuperação natural.");
                if (c.Option == "day") { RuleState.Require(s.Get("ferimento_grave") == 0, "Ferimento grave usa recuperação semanal."); s.Health += 1; return "Recuperação diária: até 1 PV."; }
                RuleState.Require(s.Get("ferimento_grave") > 0, "Recuperação semanal exige ferimento grave.");
                var roll = s.Die(100);
                if (roll > s.N("constituicao")) return "Teste de CON falhou; não houve recuperação nesta semana. O Guardião avalia complicações.";
                s.Health += s.Die(3) + (roll <= s.N("constituicao") / 5 ? s.Die(3) : 0);
                if (roll <= s.N("constituicao") / 5 || s.Health * 2 >= s.MaxHealth) s.Set("ferimento_grave", 0);
                return "Teste semanal bem-sucedido; recuperação de 1d3 PV aplicada.";
            case "dying-check": RuleState.Require(s.Get("morrendo") > 0, "O investigador não está morrendo."); if (s.Die(100) > s.N("constituicao")) s.Set("morto", 1); return s.Get("morto") > 0 ? "CON falhou: investigador morto." : "CON passou: continua morrendo; precisa de socorro.";
            case "stabilize": s.Set("morrendo", 0); return "Primeiros socorros confirmados: morrendo encerrado. Tratamento e cura precisam ser registrados separadamente.";
            case "luck-spend": RuleState.Require(s.Get("sorte_opcional") == 1 && c.Amount > 0 && s.N("sorte") >= c.Amount, "Gasto de sorte desativado, saldo insuficiente ou valor inválido."); s.Field("sorte", s.N("sorte") - c.Amount); return "Sorte descontada. Não se aplica a testes de sorte, SAN, dano ou desenvolvimento.";
            case "skill-mark": s.Set("dev_" + c.Option, 1); return "Perícia marcada para o próximo desenvolvimento.";
            case "skill-develop":
                RuleState.Require(s.Get("dev_" + c.Option) == 1, "A perícia precisa estar marcada.");
                var skill = c.Option!; var value = s.N(skill); var development = s.Die(100); s.Set("dev_" + skill, 0);
                if (development > value || development > 95) { var increase = s.Die(10); s.Field(skill, value + increase); return $"Perícia aumentou {increase} pontos."; }
                return "Perícia não aumentou nesta fase; marca removida.";
            default: throw new ArgumentException("Ação desconhecida.");
        }
    }
}
