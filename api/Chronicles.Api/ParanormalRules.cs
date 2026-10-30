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
            new("stabilize", "Encerrar morrendo", null, [new("medicine", $"Medicina DT {20 + 5 * s.Get("estabilizacoes_cena")} bem-sucedida"), new("effect", "Efeito que encerra a condição")]),
            new("calm", "Encerrar enlouquecendo", null, [new("diplomacy", $"Diplomacia DT {20 + 5 * s.Get("acalmado_cena")} bem-sucedida")]),
            new("breathe", "Voltar a respirar"), new("stop-bleeding", "Medicina DT 20 bem-sucedida contra sangramento"),
            new("sleep", "Dormir no interlúdio", null, RuleState.RestOptions),
            new("relax", "Relaxar no interlúdio", "Participantes (incluindo você)", RuleState.RestOptions),
            new("interlude", "Iniciar novo interlúdio")
        ], [new("Limite básico de PE/turno", (level + s.Get("limite_pe_bonus")).ToString()),
            new("PE gastos no turno", s.Get("pe_turno").ToString()), new("DT de rituais", (10 + level + s.N("presenca") + s.Get("dt_bonus")).ToString()),
            new("Carga sem penalidade / máxima", $"{capacity} / {capacity * 2}"),
            new("Estado", s.Get("morto") > 0 ? "Morto" : s.Get("insano") > 0 ? "Insano" : s.Get("morrendo") > 0 ? $"Morrendo ({s.Get("turnos_morrendo")}/3 turnos na cena)" : s.Health * 2 <= s.MaxHealth ? "Machucado" : "Ativo"),
            new("Estado mental", s.Get("enlouquecendo") > 0 ? $"Enlouquecendo ({s.Get("turnos_loucura")}/3 turnos na cena)" : s.Sanity * 2 <= s.MaxSanity ? "Perturbado" : "Estável")],
            [new("limite_pe_bonus", "Ajuste ao limite de PE", -20, 20), new("dt_bonus", "Ajuste à DT de rituais", -50, 50), new("carga_bonus", "Espaços extras de carga", 0, 100), new("resistencia_fogo", "Resistência aplicável ao dano de fogo", 0, 9999),
                new("condicao_conjuracao", "Conjuração: 0 normal, 1 ruim, 2 terrível", 0, 2), new("dano_durante_conjuracao", "Dano sofrido durante a execução do próximo ritual", 0, 9999)], s.State);
    }
    public static string Apply(RuleState s, RuleCommand c)
    {
        var level = Level(s);
        switch (c.Action)
        {
            case "damage":
                s.Health -= c.Amount;
                if (s.Health == 0 && c.Amount > 0) s.Set("morrendo", 1);
                if (c.Amount * 2 >= s.MaxHealth && s.Health > 0)
                {
                    var result = s.Roll(D20Checks.ParanormalExpression(s, "vigor", s.ParanormalSkill("per_fortitude"), key: "per_fortitude"));
                    var passed = Passed(s, result, 15);
                    if (!passed) { s.Health = 0; s.Set("morrendo", 1); }
                    return $"Dano massivo: Fortitude {result} / DT 15. " + (!passed ? "PV reduzidos a zero." : "Teste bem-sucedido.");
                }
                return "Dano aplicado.";
            case "heal": s.Health += c.Amount; return "Cura aplicada. A condição morrendo exige estabilização mesmo com PV positivos.";
            case "mental-damage": s.Sanity -= c.Amount; if (s.Sanity == 0) s.Set("enlouquecendo", 1); return "Dano mental aplicado.";
            case "sanity-heal": s.Sanity += c.Amount; if (s.Sanity > 0) s.Set("enlouquecendo", 0); return "Sanidade recuperada.";
            case "spend":
                RuleState.Require(!D20Conditions.CannotAct(s, physical: false), "As condições atuais impedem usar habilidades.");
                var spendCost = c.Amount + D20Conditions.CostIncrease(s);
                var limit = level + s.Get("limite_pe_bonus");
                RuleState.Require(s.Get("pe_turno") + spendCost <= limit || c.Option == "minimum" && s.Get("pe_turno") == 0, "O gasto ultrapassa o limite deste turno. A exceção exige uma habilidade no custo mínimo e nenhum gasto anterior.");
                s.SpendMana(spendCost); s.Set("pe_turno", s.Get("pe_turno") + spendCost); return "Custo descontado e registrado no turno, incluindo alquebrado quando aplicável.";
            case "ritual":
                RuleState.Require(!D20Conditions.CannotAct(s), "As condições atuais impedem conjurar rituais.");
                var ritualCost = c.Amount + D20Conditions.CostIncrease(s);
                RuleState.Require(s.Get("pe_turno") + ritualCost <= level + s.Get("limite_pe_bonus"), "O custo excede o limite de PE do turno.");
                s.SpendMana(ritualCost); s.Set("pe_turno", s.Get("pe_turno") + ritualCost);
                var circumstance = Math.Max(s.Get("condicao_conjuracao"), D20Conditions.Has(s, "agarrado") ? 2 : D20Conditions.Has(s, "caido") || D20Conditions.Has(s, "surdo") ? 1 : 0);
                var concentrationTarget = circumstance == 0 ? 0 : (circumstance == 2 ? 20 : 15) + ritualCost;
                var damageTarget = s.Get("dano_durante_conjuracao"); s.Set("dano_durante_conjuracao", 0);
                foreach (var target in new[] { concentrationTarget, damageTarget }.Where(t => t > 0))
                {
                    var concentration = s.Roll(D20Checks.ParanormalExpression(s, "presenca", s.ParanormalSkill("per_vontade"), key: "per_vontade"));
                    if (!Passed(s, concentration, target)) return $"Ritual perdido: Vontade {concentration} / DT {target}. Os {ritualCost} PE foram gastos.";
                }
                if (c.Option != "other")
                {
                    s.Sanity -= ritualCost; s.LoseSanityMaximum(c.Option == "fear-true" ? 3 : c.Option == "fear-student" ? 2 : 1);
                    if (s.Sanity == 0) s.Set("enlouquecendo", 1);
                    return "Ritual de Medo: custo em PE, perda de SAN e redução permanente aplicados. Requisitos e efeito dependem do ritual conhecido.";
                }
                var check = s.Roll(D20Checks.ParanormalExpression(s, "intelecto", s.ParanormalSkill("per_ocultismo"), key: "per_ocultismo"));
                if (!Passed(s, check, 20 + ritualCost)) { s.Sanity -= ritualCost; if (check <= 15 + ritualCost) s.LoseSanityMaximum(1); }
                if (s.Sanity == 0) s.Set("enlouquecendo", 1);
                return $"Custo paranormal: Ocultismo {check} / DT {20 + ritualCost}. Custos aplicados; o resultado deste teste não anula o ritual.";
            case "turn":
                s.Set("pe_turno", 0);
                var turnMessage = D20Conditions.StartTurn(s);
                if (D20Conditions.Has(s, "em_chamas")) { var fire = D20Conditions.FireDamage(s, true); s.Health -= fire; if (s.Health == 0 && fire > 0) s.Set("morrendo", 1); turnMessage += "Chamas resolvidas com a resistência configurada. "; }
                if (s.Get("sangrando") > 0)
                {
                    var bleed = s.Roll(D20Checks.ParanormalExpression(s, "vigor", 0));
                    if (Passed(s, bleed, 20)) s.Set("sangrando", 0);
                    else { s.Health -= s.Die(6); if (s.Health == 0) s.Set("morrendo", 1); }
                    turnMessage += $"Sangramento: Vigor {bleed} / DT 20. ";
                }
                if (D20Conditions.Has(s, "asfixiado"))
                {
                    s.Set("asfixia_rodadas", s.Get("asfixia_rodadas") + 1);
                    if (s.Get("asfixia_rodadas") > Math.Max(0, s.N("vigor")) && s.Get("asfixia_inconsciente") == 0)
                    {
                        var target = 5 + 5 * s.Get("asfixia_testes");
                        var breath = s.Roll(D20Checks.ParanormalExpression(s, "vigor", s.ParanormalSkill("per_fortitude"), key: "per_fortitude"));
                        s.Set("asfixia_testes", s.Get("asfixia_testes") + 1);
                        if (!Passed(s, breath, target)) s.Set("asfixia_inconsciente", 1);
                        turnMessage += $"Asfixia: Fortitude {breath} / DT {target}. ";
                    }
                    if (s.Get("asfixia_inconsciente") > 0) { s.Health -= s.Die(6); if (s.Health == 0) s.Set("morrendo", 1); turnMessage += "Asfixia: perda de 1d6 PV. "; }
                }
                if (s.Get("morrendo") > 0) { s.Set("turnos_morrendo", s.Get("turnos_morrendo") + 1); if (s.Get("turnos_morrendo") >= 3) s.Set("morto", 1); }
                if (s.Get("enlouquecendo") > 0) { s.Set("turnos_loucura", s.Get("turnos_loucura") + 1); if (s.Get("turnos_loucura") >= 3) s.Set("insano", 1); }
                return turnMessage + "Turno iniciado; contadores de morte e loucura avançaram quando aplicável.";
            case "stabilize": RuleState.Require(s.Get("morrendo") > 0, "O personagem não está morrendo."); s.Set("morrendo", 0); s.Set("estabilizacoes_cena", s.Get("estabilizacoes_cena") + 1); return "Morrendo encerrado. O contador da cena foi preservado.";
            case "calm": RuleState.Require(s.Get("enlouquecendo") > 0, "O personagem não está enlouquecendo."); s.Set("enlouquecendo", 0); s.Set("acalmado_cena", s.Get("acalmado_cena") + 1); return "Personagem acalmado; a próxima DT na cena aumenta em 5.";
            case "stop-bleeding": s.Set("sangrando", 0); return "Sangramento encerrado.";
            case "breathe": s.Set("cond_asfixiado", 0); s.Set("asfixia_rodadas", 0); s.Set("asfixia_testes", 0); s.Set("asfixia_inconsciente", 0); return "Respiração restabelecida; outras fontes de inconsciência permanecem.";
            case "scene": D20Conditions.EndScene(s); s.Set("turnos_morrendo", 0); s.Set("turnos_loucura", 0); s.Set("pe_turno", 0); s.Set("estabilizacoes_cena", 0); s.Set("acalmado_cena", 0); return "Nova cena iniciada; condições de cena encerradas, durações persistentes mantidas.";
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
