namespace Chronicles.Api;

public static class Dnd2014Conditions
{
    public static readonly RuleChoice[] Choices = [new("cego", "Cego"), new("enfeiticado", "Enfeitiçado"), new("surdo", "Surdo"),
        new("amedrontado", "Amedrontado"), new("agarrado", "Agarrado"), new("incapacitado", "Incapacitado"), new("invisivel", "Invisível"),
        new("paralisado", "Paralisado"), new("petrificado", "Petrificado"), new("envenenado", "Envenenado"), new("caido", "Caído"),
        new("impedido", "Impedido"), new("atordoado", "Atordoado"), new("inconsciente", "Inconsciente")];
    public static bool Has(RuleState s, string key) => s.Get("cond_" + key) > 0;
    public static bool Incapacitated(RuleState s) => s.Health == 0 || s.Get("morto") > 0 || new[] { "incapacitado", "paralisado", "petrificado", "atordoado", "inconsciente" }.Any(key => Has(s, key));
    public static void CanAttack(RuleState s) { RuleState.Require(!Incapacitated(s), "Incapacitado não pode realizar ataques."); RuleState.Require(!Has(s, "enfeiticado") || s.Get("alvo_encantador") == 0, "Enfeitiçado não pode atacar a fonte do encanto."); }
    public static int Mode(RuleState s, string kind, int manual = 0)
    {
        var fear = Has(s, "amedrontado") && s.Get("fonte_medo_oculta") == 0;
        var advantage = manual > 0 || kind == "attack" && Has(s, "invisivel") && s.Get("alvo_ve_invisivel") == 0;
        var disadvantage = manual < 0 || kind == "attack" && (Has(s, "cego") || Has(s, "caido") || Has(s, "impedido") || Has(s, "envenenado") || fear || s.Get("exaustao") >= 3)
            || kind == "ability" && (Has(s, "envenenado") || fear || s.Get("exaustao") >= 1)
            || kind == "save" && s.Get("exaustao") >= 3;
        return advantage == disadvantage ? 0 : advantage ? 1 : -1;
    }
    public static string Expression(RuleState s, string expression, string kind, int manual = 0)
    {
        RuleState.Require(DiceExpression.TryParse(expression, out var dice) && dice is { Sides: 20, Count: 1 }, "Este teste exige um d20 e modificador numérico.");
        var mode = Mode(s, kind, manual);
        return (mode == 0 ? "1d20" : mode > 0 ? "2d20kh1" : "2d20kl1") + (dice!.Modifier < 0 ? dice.Modifier.ToString() : dice.Modifier > 0 ? "+" + dice.Modifier : "");
    }
    public static RuleStat[] Stats(RuleState s)
    {
        var conditions = Choices.Where(choice => Has(s, choice.Value)).Select(choice => choice.Label).ToList();
        if (s.Get("exaustao") > 0) conditions.Add($"Exaustão {s.Get("exaustao")}");
        decimal speed = s.N("deslocamento");
        if (s.Get("exaustao") >= 2) speed /= 2;
        if (s.Get("exaustao") >= 5 || s.Health == 0 || s.Get("morto") > 0 || new[] { "agarrado", "impedido", "paralisado", "petrificado", "atordoado", "inconsciente" }.Any(key => Has(s, key))) speed = 0;
        var stats = new List<RuleStat> { new("Condições mecânicas", conditions.Count == 0 ? "Nenhuma" : string.Join(", ", conditions)), new("Deslocamento após condições", speed.ToString()), new("Concentração", s.Get("concentrando") > 0 ? "Ativa" : "Nenhuma") };
        if (Has(s, "petrificado")) stats.Add(new("Petrificado", "Resistência a todo dano; imunidade a veneno e doença"));
        if (Has(s, "caido")) stats.Add(new("Caído", "Rastejar custa movimento dobrado; levantar custa metade do deslocamento"));
        return stats.ToArray();
    }
    public static RuleAction[] Actions(SystemDefinition system) => [new("condition", "Aplicar condição", null, Choices), new("condition-remove", "Remover condição", null, Choices),
        new("exhaustion", "Definir nível de exaustão", "Nível (0–6)"), new("concentrate", "Iniciar concentração"), new("concentration-end", "Encerrar concentração"),
        new("test", "Teste de atributo, perícia ou resistência", "CD (0: só rolar)", system.Fields.Where(f => f.Type == "number" && f.RollExpression?.StartsWith("1d20", StringComparison.Ordinal) == true).Select(f => new RuleChoice(f.Key, f.Label)).ToArray()),
        new("test-expression", "Rolagem contextual", null, [new("ability", "Atributo ou perícia"), new("save", "Resistência"), new("attack", "Ataque")], Hidden: true)];
    public static bool Handles(string action) => action is "condition" or "condition-remove" or "exhaustion" or "concentrate" or "concentration-end" or "test" or "test-expression";
    public static string Apply(SystemDefinition system, RuleState s, RuleCommand command)
    {
        switch (command.Action)
        {
            case "condition": s.Set("cond_" + command.Option, 1); if (Incapacitated(s)) s.Set("concentrando", 0); return "Condição aplicada. Testes e ataques do motor consideram seus efeitos; fontes e alvos devem ser informados quando relevantes.";
            case "condition-remove": s.Set("cond_" + command.Option, 0); return "Condição removida.";
            case "exhaustion":
                RuleState.Require(command.Amount is >= 0 and <= 6, "Exaustão vai de 0 a 6.");
                var old = s.Get("exaustao");
                if (old < 4 && command.Amount >= 4) { s.Set("pv_max_sem_exaustao", s.MaxHealth); s.ChangeHealthMaximum(s.MaxHealth / 2); }
                if (old >= 4 && command.Amount < 4 && s.Get("pv_max_sem_exaustao") > 0) { s.ChangeHealthMaximum(s.Get("pv_max_sem_exaustao")); s.Set("pv_max_sem_exaustao", 0); }
                s.Set("exaustao", command.Amount); if (command.Amount == 6) { s.Set("morto", 1); s.Set("concentrando", 0); }
                return "Exaustão atualizada; efeitos cumulativos sobre testes, deslocamento e PV foram aplicados.";
            case "concentrate": RuleState.Require(!Incapacitated(s), "Incapacitado não pode manter concentração."); s.Set("concentrando", 1); return "Concentração iniciada; a anterior foi encerrada.";
            case "concentration-end": s.Set("concentrando", 0); return "Concentração encerrada.";
            case "test":
                var field = system.Fields.Single(f => f.Key == command.Option);
                var save = field.Key.StartsWith("res_", StringComparison.Ordinal);
                if (save && (field.Key is "res_forca" or "res_destreza") && (s.Health == 0 || new[] { "paralisado", "petrificado", "atordoado", "inconsciente" }.Any(key => Has(s, key)))) return $"{field.Label}: falha automática pela condição.";
                if (field.Key == "per_percepcao" && (s.Get("sentido_percepcao") == 1 && Has(s, "cego") || s.Get("sentido_percepcao") == 2 && Has(s, "surdo"))) return $"{field.Label}: falha automática no sentido necessário.";
                var expression = Expression(s, WeaponActions.Resolve(field.RollExpression!, s, false), save ? "save" : "ability", s.Get("modo_testes"));
                var result = s.Roll(expression);
                return $"{field.Label}: {expression} = {result}" + (command.Amount > 0 ? $" / CD {command.Amount}: {(result >= command.Amount ? "sucesso" : "falha")}." : ".");
            case "test-expression":
                RuleState.Require(command.Expression is { Length: > 0 and <= 40 }, "Informe expressão numérica de um d20.");
                if (command.Option == "attack") CanAttack(s);
                var contextual = Expression(s, command.Expression!, command.Option!, command.Option == "attack" ? s.Get("modo_ataque") : s.Get("modo_testes"));
                return $"{contextual} = {s.Roll(contextual)}.";
            default: throw new ArgumentException("Ação desconhecida.");
        }
    }
    public static string CheckConcentration(RuleState s, int damage)
    {
        if (s.Get("concentrando") == 0 || damage <= 0) return "";
        if (Incapacitated(s)) { s.Set("concentrando", 0); return " Concentração encerrada por incapacidade."; }
        var bonus = s.N("res_constituicao");
        var expression = Expression(s, "1d20" + (bonus < 0 ? bonus.ToString() : bonus > 0 ? "+" + bonus : ""), "save", s.Get("vantagem_concentracao"));
        var result = s.Roll(expression); var dc = Math.Max(10, damage / 2);
        if (result < dc) s.Set("concentrando", 0);
        return $" Concentração: Constituição {result} / CD {dc}; {(result >= dc ? "mantida" : "encerrada")}.";
    }
}
