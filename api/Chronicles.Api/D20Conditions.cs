namespace Chronicles.Api;

// Shared condition relationships; the two editions resolve their penalties separately.
public static class D20Conditions
{
    private static readonly RuleChoice[] Common = [new("abalado", "Abalado"), new("agarrado", "Agarrado"), new("alquebrado", "Alquebrado"),
        new("apavorado", "Apavorado"), new("atordoado", "Atordoado"), new("caido", "Caído"), new("cego", "Cego"), new("confuso", "Confuso"),
        new("debilitado", "Debilitado"), new("desprevenido", "Desprevenido"), new("doente", "Doente"), new("em_chamas", "Em chamas"),
        new("enjoado", "Enjoado"), new("enredado", "Enredado"), new("envenenado", "Envenenado"), new("esmorecido", "Esmorecido"),
        new("exausto", "Exausto"), new("fascinado", "Fascinado"), new("fatigado", "Fatigado"), new("fraco", "Fraco"), new("frustrado", "Frustrado"),
        new("imovel", "Imóvel"), new("inconsciente", "Inconsciente"), new("indefeso", "Indefeso"), new("lento", "Lento"), new("ofuscado", "Ofuscado"),
        new("paralisado", "Paralisado"), new("pasmo", "Pasmo"), new("petrificado", "Petrificado"), new("sangrando", "Sangrando"),
        new("sobrecarregado", "Sobrecarregado"), new("surdo", "Surdo"), new("surpreendido", "Surpreendido"), new("vulneravel", "Vulnerável")];
    public static RuleChoice[] Choices(bool paranormal) => [.. Common, paranormal ? new("asfixiado", "Asfixiado") : new("enfeiticado", "Enfeitiçado")];
    private static string Key(string condition) => condition == "sangrando" ? "sangrando" : "cond_" + condition;
    public static bool Has(RuleState s, string condition) => Effective(s).Contains(condition);
    public static HashSet<string> Effective(RuleState s)
    {
        var active = Common.Select(c => c.Value).Concat(["asfixiado", "enfeiticado"]).Where(c => s.Get(Key(c)) != 0).ToHashSet(StringComparer.Ordinal);
        if ((s.Health == 0 && s.Get("age_pv_zero") == 0) || s.Get("morto") > 0 || s.Get("asfixia_inconsciente") > 0) active.Add("inconsciente");
        var relationships = new Dictionary<string, string[]> {
            ["agarrado"] = ["desprevenido", "imovel"], ["atordoado"] = ["desprevenido"], ["cego"] = ["desprevenido", "lento"],
            ["enredado"] = ["lento", "vulneravel"], ["exausto"] = ["debilitado", "lento", "vulneravel"], ["fatigado"] = ["fraco", "vulneravel"],
            ["inconsciente"] = ["indefeso"], ["indefeso"] = ["desprevenido"], ["paralisado"] = ["imovel", "indefeso"],
            ["petrificado"] = ["inconsciente"], ["surpreendido"] = ["desprevenido"] };
        bool changed;
        do { changed = false; foreach (var pair in relationships) if (active.Contains(pair.Key)) foreach (var implied in pair.Value) changed |= active.Add(implied); } while (changed);
        return active;
    }
    public static RuleAction[] Actions(bool paranormal) => [new("condition", "Aplicar condição", "Rodadas (0: até fim da cena)", Choices(paranormal)),
        new("condition-persistent", "Aplicar condição sem duração de cena", null, Choices(paranormal)), new("condition-remove", "Remover condição", null, Choices(paranormal)),
        new("condition-round", "Avançar durações ao concluir a rodada")];
    public static bool Handles(string action) => action is "condition" or "condition-persistent" or "condition-remove" or "condition-round";
    public static string Apply(RuleState s, RuleCommand command)
    {
        if (command.Action == "condition-round")
        {
            foreach (var key in s.State.Keys.Where(key => key.StartsWith("cond_", StringComparison.Ordinal)).ToArray())
                if (s.Get(key) >= 2) { var expired = s.Get(key) == 2; s.Set(key, expired ? 0 : s.Get(key) - 1); if (expired && key == "cond_asfixiado") EndAsphyxia(s); }
            return "Durações de rodada avançadas; condições da cena e persistentes foram mantidas.";
        }
        var condition = command.Option!;
        if (command.Action == "condition-remove") { s.Set(Key(condition), 0); if (condition == "asfixiado") EndAsphyxia(s); return "Condição removida; efeitos de outras fontes ainda se aplicam."; }
        var active = Effective(s);
        var upgrade = condition switch {
            "abalado" when active.Contains("abalado") || active.Contains("apavorado") => "apavorado",
            "fraco" when active.Contains("debilitado") => "inconsciente", "fraco" when active.Contains("fraco") => "debilitado",
            "debilitado" when active.Contains("debilitado") => "inconsciente",
            "frustrado" when active.Contains("frustrado") || active.Contains("esmorecido") => "esmorecido",
            "fatigado" when active.Contains("exausto") => "inconsciente", "fatigado" when active.Contains("fatigado") => "exausto",
            "exausto" when active.Contains("exausto") => "inconsciente", _ => condition };
        // 1 = scene, -1 = persistent, N+1 = N rounds. Bleeding has its own recovery rule.
        var duration = upgrade == "sangrando" ? 1 : command.Action == "condition-persistent" ? -1 : command.Amount == 0 ? 1 : command.Amount + 1;
        var previous = s.Get(Key(upgrade));
        if (previous == -1 || duration == -1) duration = -1;
        else if (previous == 1 || duration == 1) duration = 1;
        else duration = Math.Max(previous, duration);
        s.Set(Key(upgrade), duration);
        return upgrade == condition ? "Condição aplicada." : $"Condição agravada: {upgrade}.";
    }
    public static void EndScene(RuleState s)
    {
        foreach (var key in s.State.Keys.Where(key => key.StartsWith("cond_", StringComparison.Ordinal)).ToArray()) if (s.Get(key) == 1) { s.Set(key, 0); if (key == "cond_asfixiado") EndAsphyxia(s); }
        s.Set("confusao_turno", 0);
    }
    public static bool CannotAct(RuleState s, bool physical = true)
    {
        var active = Effective(s);
        return s.Get("morto") > 0 || new[] { "inconsciente", "atordoado", "fascinado", "pasmo", "surpreendido" }.Any(active.Contains)
            || physical && active.Contains("paralisado") || s.Get("confusao_turno") == 2;
    }
    public static int Severity(RuleState s, string attribute, string key, bool attack, bool ranged, bool paranormal)
    {
        var active = Effective(s); var skill = attack || key.StartsWith("per_", StringComparison.Ordinal);
        var severity = skill && active.Contains("apavorado") ? 2 : (paranormal || skill) && active.Contains("abalado") ? 1 : 0;
        var physical = (paranormal ? new[] { "agilidade", "forca", "vigor" } : new[] { "forca", "destreza", "constituicao" }).Contains(attribute);
        var mental = (paranormal ? new[] { "intelecto", "presenca" } : new[] { "inteligencia", "sabedoria", "carisma" }).Contains(attribute);
        if (physical) severity = Math.Max(severity, active.Contains("debilitado") ? 2 : active.Contains("fraco") ? 1 : 0);
        if (mental) severity = Math.Max(severity, active.Contains("esmorecido") ? 2 : active.Contains("frustrado") ? 1 : 0);
        if (skill && (attribute is "forca" or "agilidade" or "destreza") && active.Contains("cego")) severity = Math.Max(severity, 2);
        if (key == "per_percepcao") severity = Math.Max(severity, active.Contains("fascinado") ? 2 : active.Contains("ofuscado") ? 1 : 0);
        if (key == "per_reflexos" && active.Contains("desprevenido")) severity = Math.Max(severity, paranormal ? 1 : 2);
        if ((key is "per_iniciativa" or "iniciativa") && active.Contains("surdo")) severity = Math.Max(severity, 2);
        if (attack) severity = Math.Max(severity, !ranged && active.Contains("caido") ? 2 : active.Contains("agarrado") || active.Contains("enredado") || active.Contains("ofuscado") ? 1 : 0);
        return severity;
    }
    public static int Defense(RuleState s, bool ranged, bool paranormal)
    {
        var active = Effective(s); var penalty = active.Contains("indefeso") ? 10 : active.Contains("desprevenido") ? 5 : active.Contains("vulneravel") ? 2 : 0;
        if (paranormal && active.Contains("sobrecarregado")) penalty = Math.Max(penalty, 5);
        var prone = active.Contains("caido") ? ranged ? 5 : -5 : 0;
        return s.N("defesa") - (prone < 0 && paranormal ? Math.Max(penalty, -prone) : penalty) + (prone < 0 && paranormal ? 0 : prone);
    }
    public static decimal Speed(RuleState s)
    {
        var active = Effective(s); if (active.Contains("imovel") || active.Contains("inconsciente")) return 0;
        decimal speed = s.N("deslocamento");
        if (active.Contains("lento")) speed = Math.Floor(speed / 3) * 1.5m;
        if (active.Contains("sobrecarregado")) speed = Math.Max(0, speed - 3);
        if (active.Contains("caido")) speed = Math.Min(speed, 1.5m);
        return speed;
    }
    public static int CostIncrease(RuleState s) => Has(s, "alquebrado") ? 1 : 0;
    private static void EndAsphyxia(RuleState s) { s.Set("asfixia_rodadas", 0); s.Set("asfixia_testes", 0); s.Set("asfixia_inconsciente", 0); }

    public static RuleStat[] Stats(RuleState s, bool paranormal) => [
        new("Condições efetivas", string.Join(", ", Choices(paranormal).Where(c => Has(s, c.Value)).Select(c => c.Label)) is { Length: > 0 } labels ? labels : "Nenhuma"),
        new("Defesa corpo a corpo / distância", $"{Defense(s, false, paranormal)} / {Defense(s, true, paranormal)}"),
        new("Deslocamento após condições", Speed(s).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new("Proteção por petrificação", Has(s, "petrificado") ? paranormal ? "Resistência 10" : "Redução 8" : "—")];

    public static int FireDamage(RuleState s, bool paranormal)
    {
        var reduction = Math.Max(s.Get("resistencia_fogo"), Has(s, "petrificado") ? paranormal ? 10 : 8 : 0);
        return Math.Max(0, s.Die(6) - reduction);
    }

    public static string StartTurn(RuleState s)
    {
        s.Set("confusao_turno", 0);
        if (!Has(s, "confuso")) return "";
        var roll = s.Die(6);
        if (roll == 6) { s.Set("cond_confuso", 0); return "Confusão encerrada. "; }
        s.Set("confusao_turno", roll == 1 ? 1 : roll <= 3 ? 2 : 3);
        return roll == 1 ? $"Confusão: movimento na direção {s.Die(8)} de 8. " : roll <= 3 ? "Confusão: sem ações neste turno. " : "Confusão: ataque o ser mais próximo; se estiver sozinho, aplique o dano a si. ";
    }
}
