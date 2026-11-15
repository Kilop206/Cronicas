namespace Chronicles.Domain;

public static class FmConditions
{
    public static HashSet<string> Active(RuleState s)
    {
        var active = FmCatalog.Conditions.Where(c => s.Get("cond_" + c) != 0).ToHashSet();
        if (s.Get("exaustao") >= 2) active.Add("desprevenido");
        if (s.Get("exaustao") >= 3 || SoulStage(s) >= 2) active.Add("exposto");
        if (s.Get("exaustao") >= 4) { active.Add("condenado"); active.Add("desorientado"); }
        if (s.Get("exaustao") >= 5) active.Add("enjoado");
        if (SoulStage(s) >= 3) active.Add("fragilizado");
        if (s.Get("morrendo") != 0 || s.Get("desmaiado") != 0 || s.Get("morto") != 0) active.Add("inconsciente");
        if(s.Get("ritual_mode")==2) active.Add("desprevenido");
        var changed = true;
        while (changed)
        {
            var before = active.Count;
            if (active.Contains("cego")) { active.Add("surpreso"); active.Add("lento"); }
            if (active.Contains("surpreso") || active.Contains("agarrado") || active.Contains("atordoado")) active.Add("desprevenido");
            if (active.Contains("agarrado") || active.Contains("indefeso")) active.Add("imovel");
            if (active.Contains("indefeso")) active.Add("atordoado");
            if (active.Contains("inconsciente")) active.Add("caido");
            changed = before != active.Count;
        }
        if(s.Fields.TryGetValue("imunidades_condicao",out var immune) && immune.ValueKind==System.Text.Json.JsonValueKind.Array)
            active.ExceptWith(immune.EnumerateArray().Where(v=>v.ValueKind==System.Text.Json.JsonValueKind.String).Select(v=>v.GetString()!));
        if(s.Text("origem")=="corpo_mutante")active.Remove("envenenado");
        return active;
    }
    public static int SoulStage(RuleState s)
    {
        var maximum = s.Get("alma_max", s.MaxHealth); var current = maximum - s.Get("alma_dano");
        return current * 4 < maximum ? 3 : current * 2 < maximum ? 2 : current * 4 < maximum * 3 ? 1 : 0;
    }
    public static int SoulPenalty(RuleState s) => new[] { 0, 3, 6, 8 }[SoulStage(s)];
    public static int OutputPenalty(RuleState s) => s.Get("exaustao")+SoulPenalty(s);
    public static int CostPenalty(RuleState s) => new[] { 0, 2, 3, 5 }[SoulStage(s)] + (Active(s).Contains("condenado") ? 1 : 0);
    public static bool Incapacitated(RuleState s) => Active(s).Overlaps(["inconsciente", "atordoado", "paralisado"]);
    public static int Defense(RuleState s, bool ranged = false)
    {
        var active = Active(s);
        var penalty = active.Contains("paralisado") ? 10 : active.Overlaps(["desprevenido"]) || active.Contains("caido") && !ranged ? 3 : active.Contains("enredado") ? 2 : 0;
        if (Overloaded(s)) penalty = Math.Max(penalty, 5);
        var restricted=FmCatalog.ClassLevel(s,"restringido");var restrictedBonus=restricted>0?Math.Min(FmCatalog.Level(s),Math.Max(0,FmCatalog.Mod(s,s.Text("defesa_restrita")=="constituicao"?"constituicao":"forca")))+(restricted>=3?1:0)+(restricted>=9?1:0)+(restricted>=16?1:0):0;
        return s.N("defesa", 10) + restrictedBonus + s.Get("defesa_empolgacao") + s.Get("defesa_descendente") - penalty - s.Get("exaustao") - Math.Min(5, s.Get("sequencia") / 2) + (active.Contains("caido") && ranged ? 3 : 0);
    }
    public static bool Overloaded(RuleState s) => s.N("carga_meios") > Math.Max(0, 8 + 2 * FmCatalog.Mod(s, "forca")) * 2;
    public static decimal Speed(RuleState s)
    {
        var active = Active(s);
        if (active.Contains("imovel") || Incapacitated(s)) return 0;
        var speed = (decimal)s.N("deslocamento", 9) + (s.Text("origem")=="restringido"?3:0)+(FmCatalog.ClassLevel(s,"restringido")>=10?3:0) - 1.5m * s.Get("exaustao") - (Overloaded(s) ? 4.5m : 0) - (active.Contains("sofrendo") ? 3 : 0);
        if (active.Overlaps(["lento", "enredado"])) speed /= 2;
        if (s.Get("dominio") > 0 && s.Get("confronto") == 0) speed *= 2;
        return Math.Max(0, active.Contains("caido") ? Math.Min(4.5m, speed) : speed);
    }
    public static int Training(RuleState s, string key, bool save = false) => FmCatalog.Rows(s, save ? "resistencias" : "pericias").Where(r => FmCatalog.Text(r, "chave") == key).Select(r => FmCatalog.Number(r, "treino")).FirstOrDefault();
    public static int Bonus(RuleState s, FmCheck check, bool save = false)
    {
        var row = FmCatalog.Rows(s, save ? "resistencias" : "pericias").FirstOrDefault(r => FmCatalog.Text(r, "chave") == check.Id);
        var training = row.ValueKind == System.Text.Json.JsonValueKind.Object ? FmCatalog.Number(row, "treino") : 0;
        var extra = row.ValueKind == System.Text.Json.JsonValueKind.Object ? FmCatalog.Number(row, "outros") : 0;
        var bt = FmCatalog.Training(s); var levels = FmCatalog.Levels(s); var restricted = levels.GetValueOrDefault("restringido");
        var mastery = restricted >= 10 && check.Attribute is "forca" or "destreza" or "constituicao" ? bt : bt / 2;
        if (restricted >= 9 && save && check.Id is "fortitude" or "reflexos") training = 2;
        var bonus = FmCatalog.Mod(s, check.Attribute) + FmCatalog.Level(s) / 2 + (training > 0 ? bt : 0) + (training == 2 ? mastery : 0) + extra;
        if (restricted >= 2 && !save) bonus += restricted >= 10 ? 2 : 1;
        if (restricted >= 3 && save && check.Id == "reflexos") bonus += 1 + (restricted >= 9 ? 1 : 0) + (restricted >= 16 ? 1 : 0);
        if(save&&check.Id=="fortitude"&&FmCatalog.ClassLevel(s,"lutador")>=5){var effect=FmCatalog.EffectLevel(s,"lutador");bonus+=1+(effect>=9?1:0)+(effect>=13?1:0)+(effect>=17?1:0);}
        var active = Active(s);
        bonus -= s.Get("exaustao") + SoulPenalty(s) + Math.Max(active.Contains("envenenado") ? 2 : 0, !save ? active.Contains("amedrontado") ? 3 : active.Contains("abalado") ? 1 : 0 : 0);
        if (save && check.Id == "reflexos" && active.Contains("desprevenido")) bonus -= 3;
        if (!save && check.Attribute == "destreza") bonus -= s.N("penalidade_uniforme") + s.N("penalidade_escudo");
        if (!save && check.Id == "percepcao" && active.Contains("cego")) bonus -= 5;
        if (!save && check.Id == "furtividade" && active.Contains("invisivel")) bonus += 10;
        return bonus;
    }
    public static int Roll(RuleState s, int bonus, bool advantage = false, bool disadvantage = false)
    {
        advantage |= s.Get("modo_teste") == 1;
        disadvantage |= s.Get("modo_teste") == -1 || SoulStage(s) >= 3;
        var expression = advantage == disadvantage ? "1d20" : advantage ? "2d20kh1" : "2d20kl1";
        s.Roll(expression + (bonus >= 0 ? "+" : "") + bonus);
        return s.Rolls[^1].Rolls[s.Rolls[^1].KeptIndexes[0]];
    }
    // 0 = failure, 1 = success, 2 = master's critical success (p.281).
    public static int Save(RuleState s, string key, int dc, bool concentration = false)
    {
        var check = FmCatalog.Saves.Single(c => c.Id == key); var active = Active(s);
        if (key == "reflexos" && active.Overlaps(["inconsciente", "paralisado"])) return 0;
        var bonus = Bonus(s, check, true) - (concentration && active.Contains("sofrendo") ? 5 : 0);
        var natural = Roll(s, bonus); var total = s.Rolls[^1].Total;
        var master = Training(s, key, true) == 2 || FmCatalog.Levels(s).GetValueOrDefault("restringido") >= 9 && key is "fortitude" or "reflexos";
        var result = total >= dc ? master && total >= dc + 10 ? 2 : 1 : 0;
        if (natural == 20) result = Math.Min(master ? 2 : 1, result + 1);
        return result;
    }
}
