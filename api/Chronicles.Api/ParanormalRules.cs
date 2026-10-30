namespace Chronicles.Api;

public static class ParanormalRules
{
    private static int Level(RuleState s)
    {
        var nex = s.N("nex");
        RuleState.Require(nex == 99 || nex is >= 5 and <= 95 && nex % 5 == 0, "Configure um NEX válido: 5 a 95 em passos de 5, ou 99.");
        return nex == 99 ? 20 : nex / 5;
    }
    public static RuleView Describe(RuleState s)
    {
        var nex = s.N("nex");
        var level = nex == 99 ? 20 : Math.Clamp(nex / 5, 0, 19);
        var capacity = (s.N("forca") == 0 ? 2 : s.N("forca") * 5) + s.Get("carga_bonus");
        return new("Ordem Paranormal 1.3", [
            new("damage", "Sofrer dano", "Dano após resistências"), new("heal", "Receber cura", "PV"),
            new("mental-damage", "Sofrer dano mental", "SAN"), new("sanity-heal", "Recuperar sanidade", "SAN"),
            new("spend", "Usar PE", "Custo", [new("normal", "Custo normal"), new("minimum", "Custo mínimo de uma habilidade")]),
            new("ritual", "Conjurar ritual conhecido e pagar o custo paranormal", "Custo total em PE", [new("other", "Elemento diferente de Medo"), new("fear", "Medo: forma normal"), new("fear-student", "Medo: discente"), new("fear-true", "Medo: verdadeira")]),
            new("turn", "Iniciar turno"), new("scene", "Iniciar nova cena"),
            new("stabilize", "Encerrar morrendo", null, [new("medicine", "Medicina DT 20 bem-sucedida"), new("effect", "Efeito que encerra a condição")]),
            new("sleep", "Dormir no interlúdio", null, RuleState.RestOptions),
            new("relax", "Relaxar no interlúdio", "Participantes (incluindo você)", RuleState.RestOptions),
            new("interlude", "Iniciar novo interlúdio")
        ], [new("Limite básico de PE/turno", (level + s.Get("limite_pe_bonus")).ToString()),
            new("PE gastos no turno", s.Get("pe_turno").ToString()), new("DT de rituais", (10 + level + s.N("presenca") + s.Get("dt_bonus")).ToString()),
            new("Carga sem penalidade / máxima", $"{capacity} / {capacity * 2}"),
            new("Estado", s.Get("morto") > 0 ? "Morto" : s.Get("insano") > 0 ? "Insano" : s.Get("morrendo") > 0 ? $"Morrendo ({s.Get("turnos_morrendo")}/3 turnos na cena)" : s.Health * 2 <= s.MaxHealth ? "Machucado" : "Ativo"),
            new("Estado mental", s.Get("enlouquecendo") > 0 ? $"Enlouquecendo ({s.Get("turnos_loucura")}/3 turnos na cena)" : s.Sanity * 2 <= s.MaxSanity ? "Perturbado" : "Estável")],
            [new("limite_pe_bonus", "Ajuste ao limite de PE", -20, 20), new("dt_bonus", "Ajuste à DT de rituais", -50, 50), new("carga_bonus", "Espaços extras de carga", 0, 100)], s.State);
    }
    public static string Apply(RuleState s, RuleCommand c)
    {
        var level = Level(s);
        switch (c.Action)
        {
            case "damage":
                s.Health -= c.Amount;
                if (s.Health == 0) s.Set("morrendo", 1);
                if (c.Amount * 2 >= s.MaxHealth && s.Health > 0)
                {
                    var vigor = s.N("vigor"); var bonus = s.ParanormalSkill("per_fortitude");
                    var result = s.Roll($"{(vigor > 0 ? vigor : 2)}d20{(vigor == 0 ? "kl1" : vigor > 1 ? "kh1" : "")}{(bonus < 0 ? bonus.ToString() : "+" + bonus)}");
                    var passed = Passed(s, result, 15);
                    if (!passed) { s.Health = 0; s.Set("morrendo", 1); }
                    return $"Dano massivo: Fortitude {result} / DT 15. " + (!passed ? "PV reduzidos a zero." : "Teste bem-sucedido.");
                }
                return "Dano aplicado.";
            case "heal": s.Health += c.Amount; return "Cura aplicada. A condição morrendo exige estabilização mesmo com PV positivos.";
            case "mental-damage": s.Sanity -= c.Amount; if (s.Sanity == 0) s.Set("enlouquecendo", 1); return "Dano mental aplicado.";
            case "sanity-heal": s.Sanity += c.Amount; if (s.Sanity > 0) s.Set("enlouquecendo", 0); return "Sanidade recuperada.";
            case "spend":
                var limit = level + s.Get("limite_pe_bonus");
                RuleState.Require(s.Get("pe_turno") + c.Amount <= limit || c.Option == "minimum" && s.Get("pe_turno") == 0, "O gasto ultrapassa o limite deste turno. A exceção exige uma habilidade no custo mínimo e nenhum gasto anterior.");
                s.SpendMana(c.Amount); s.Set("pe_turno", s.Get("pe_turno") + c.Amount); return "Custo descontado e registrado no turno.";
            case "ritual":
                RuleState.Require(s.Get("pe_turno") + c.Amount <= level + s.Get("limite_pe_bonus"), "O custo excede o limite de PE do turno.");
                s.SpendMana(c.Amount); s.Set("pe_turno", s.Get("pe_turno") + c.Amount);
                if (c.Option != "other")
                {
                    s.Sanity -= c.Amount; s.LoseSanityMaximum(c.Option == "fear-true" ? 3 : c.Option == "fear-student" ? 2 : 1);
                    if (s.Sanity == 0) s.Set("enlouquecendo", 1);
                    return "Ritual de Medo: custo em PE, perda de SAN e redução permanente aplicados. Requisitos e efeito dependem do ritual conhecido.";
                }
                var intellect = s.N("intelecto"); var occult = s.ParanormalSkill("per_ocultismo");
                var check = s.Roll($"{(intellect > 0 ? intellect : 2)}d20{(intellect == 0 ? "kl1" : intellect > 1 ? "kh1" : "")}{(occult < 0 ? occult.ToString() : "+" + occult)}");
                if (!Passed(s, check, 20 + c.Amount)) { s.Sanity -= c.Amount; if (check <= 15 + c.Amount) s.LoseSanityMaximum(1); }
                if (s.Sanity == 0) s.Set("enlouquecendo", 1);
                return $"Custo paranormal: Ocultismo {check} / DT {20 + c.Amount}. Custos aplicados; o resultado deste teste não anula o ritual.";
            case "turn":
                s.Set("pe_turno", 0);
                if (s.Get("morrendo") > 0) { s.Set("turnos_morrendo", s.Get("turnos_morrendo") + 1); if (s.Get("turnos_morrendo") >= 3) s.Set("morto", 1); }
                if (s.Get("enlouquecendo") > 0) { s.Set("turnos_loucura", s.Get("turnos_loucura") + 1); if (s.Get("turnos_loucura") >= 3) s.Set("insano", 1); }
                return "Turno iniciado; contadores de morte e loucura avançaram quando aplicável.";
            case "stabilize": s.Set("morrendo", 0); return "Morrendo encerrado. O contador da cena foi preservado.";
            case "scene": s.Set("turnos_morrendo", 0); s.Set("turnos_loucura", 0); s.Set("pe_turno", 0); return "Contadores da nova cena reiniciados.";
            case "sleep":
                RuleState.Require(s.Get("dormiu") == 0, "Dormir já foi aplicado neste interlúdio.");
                var recovery = RuleState.Recovery(level, c.Option);
                s.Health += recovery; s.Mana += recovery; s.Set("dormiu", 1); return $"Recuperação aplicada: até {recovery} PV e PE.";
            case "relax":
                RuleState.Require(s.Get("relaxou") == 0 && c.Amount is >= 1 and <= 50, "Informe de 1 a 50 participantes; só é possível relaxar uma vez por interlúdio.");
                var san = RuleState.Recovery(level, c.Option) + c.Amount;
                s.Sanity += san; if (s.Sanity > 0) s.Set("enlouquecendo", 0); s.Set("relaxou", 1); return $"Recuperação aplicada: até {san} SAN.";
            case "interlude": s.Set("dormiu", 0); s.Set("relaxou", 0); return "Novo interlúdio iniciado.";
            default: throw new ArgumentException("Ação desconhecida.");
        }
    }
    private static bool Passed(RuleState s, int total, int target) { var roll = s.Rolls[^1]; return roll.Rolls[roll.KeptIndexes[0]] == 20 || total >= target; }
}
