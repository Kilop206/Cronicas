namespace Chronicles.Domain;

public sealed record WeaponContext(bool Reckless = false, bool OutsideTurn = false, int BrutalDie = 0);

// These rules use explicit weapon/turn context, never names of items or classes.
public static class Dnd2014BarbarianAttacks
{
    public static bool Reckless(RuleState s) => s.Get("descuidado_turno") > 0;
    public static int BrutalDice(RuleState s) => s.Get("nivel_barbaro") >= 17 ? 3 : s.Get("nivel_barbaro") >= 13 ? 2 : s.Get("nivel_barbaro") >= 9 ? 1 : 0;
    public static void StartTurn(RuleState s)
    {
        s.Set("ataques_turno", 0); s.Set("descuidado_turno", 0); s.Set("descuidado_exposto", 0);
    }
    public static void EndTurn(RuleState s) => s.Set("descuidado_turno", 0);
    public static void ConfirmPreviousAttack(RuleState s) => s.Set("ataques_turno", Math.Max(1, s.Get("ataques_turno")));
    public static void Prepare(RuleState s, WeaponContext? context)
    {
        if (context?.Reckless == true)
        {
            RuleState.Require(s.Get("nivel_barbaro") >= 2 && s.Get("ataques_turno") == 0 && !context.OutsideTurn,
                "Ataque Descuidado exige bárbaro 2 e deve ser escolhido no primeiro ataque do próprio turno.");
            s.Set("descuidado_turno", 1); s.Set("descuidado_exposto", 1);
        }
        if (context?.OutsideTurn != true)
        {
            RuleState.Require(s.Get("ataques_turno") < 9999, "Confirme o início de um novo turno antes de atacar novamente.");
            s.Set("ataques_turno", s.Get("ataques_turno") + 1);
        }
    }
    public static bool Advantage(RuleState s, string type, string ability, WeaponContext? context) =>
        Reckless(s) && s.Get("nivel_barbaro") >= 2 && type == "corpo" && ability == "mod_forca" && context?.OutsideTurn != true;
    public static int WeaponDie(RuleState s, string type, string formula, WeaponContext? context)
    {
        RuleState.Require(context is null || context.BrutalDie is 0 or 3 or 4 or 6 or 8 or 10 or 12 or 20 or 100, "Dado de Crítico Brutal inválido.");
        if (type != "corpo" || BrutalDice(s) == 0) return 0;
        var dice = DamageFormula.Parse(formula, s).Where(term => term.Count > 0 && term.Sign > 0).Select(term => term.Sides).Distinct().ToArray();
        if (dice.Length == 0)
        {
            RuleState.Require(context?.BrutalDie is null or 0, "A fórmula base não contém o dado de arma escolhido.");
            return 0;
        }
        if (context is { BrutalDie: > 0 })
        {
            RuleState.Require(dice.Contains(context.BrutalDie), "O dado de Crítico Brutal deve estar no dano base da arma, não no dano extra.");
            return context.BrutalDie;
        }
        RuleState.Require(dice.Length == 1, "Dano base com dados diferentes: escolha o dado da arma para Crítico Brutal e mantenha efeitos adicionais no dano extra.");
        return dice[0];
    }
    public static string? Validate(RuleState s)
    {
        if (s.Get("ataques_turno") is < 0 or > 9999 || new[] { "descuidado_turno", "descuidado_exposto" }.Any(key => s.Get(key) is < 0 or > 1)) return "Estado de Ataque Descuidado inválido.";
        return null;
    }
}
