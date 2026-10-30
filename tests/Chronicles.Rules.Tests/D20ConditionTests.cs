using System.Text.Json;
using Chronicles.Api;
using static Chronicles.Api.CharacterEndpoints;

public static class D20ConditionTestCases
{
    public static void Run()
    {
        var fields = new[] { new SystemField("estado_regras", "Estado", "table", "Regras", null, null, null),
            N("forca", "{forca}d20kh1"), N("vigor", "{vigor}d20kh1"), N("per_luta", "{forca}d20kh1+{per_luta}"),
            N("per_reflexos", "{forca}d20kh1+{per_reflexos}"), N("per_fortitude", "{vigor}d20kh1+{per_fortitude}"),
            N("defesa"), N("deslocamento") };
        var op = new SystemDefinition(fields, null, "ordem_paranormal", 7, "1.3");
        var t20 = new SystemDefinition([.. fields.Select(f => f.Key == "per_luta" ? f with { RollExpression = "1d20+{per_luta}", Formula = "forca + 2" } : f), N("per_vontade", "1d20+{per_vontade}"), N("per_misticismo", "1d20+{per_misticismo}")], null, "tormenta20", 4, "jogo-do-ano-2024-v1.3");
        CharacterInput Make() => new("Teste", null, null, 20, 30, 50, 99, 10, 20, new Dictionary<string, JsonElement> {
            ["forca"] = JsonSerializer.SerializeToElement(3), ["vigor"] = JsonSerializer.SerializeToElement(2), ["nex"] = JsonSerializer.SerializeToElement(35),
            ["per_luta"] = JsonSerializer.SerializeToElement(5), ["per_reflexos"] = JsonSerializer.SerializeToElement(0), ["per_fortitude"] = JsonSerializer.SerializeToElement(0),
            ["nivel"] = JsonSerializer.SerializeToElement(2), ["defesa"] = JsonSerializer.SerializeToElement(15), ["deslocamento"] = JsonSerializer.SerializeToElement(9),
            ["per_vontade"] = JsonSerializer.SerializeToElement(0), ["per_misticismo"] = JsonSerializer.SerializeToElement(0) });
        RuleResult Apply(SystemDefinition system, CharacterInput c, string action, string? option = null, int amount = 0, int die = 10) => SheetRuleActions.Apply(system, c, new(action, amount, option), sides => Math.Min(die, sides));
        void Check(bool test, string label) { if (!test) throw new InvalidOperationException("D20 conditions: " + label); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected condition rejection."); }
        var weak = Apply(op, Make(), "condition", "fraco").Character;
        var afraid = Apply(op, weak, "condition", "abalado").Character;
        Check(Apply(op, afraid, "test", "per_luta").Rolls[0].Expression == "2d20kh1+5", "same severity penalties do not stack");
        Check(SheetRuleActions.Describe(op, afraid).Checks!["per_luta"].Expression == "2d20kh1+5", "display preview uses the authoritative condition expression");
        var debilitated = Apply(op, afraid, "condition", "fraco").Character;
        Check(Apply(op, debilitated, "test", "per_luta").Rolls[0].Expression == "1d20+5", "repeated weak escalates and subtracts two dice");
        var unconscious = Apply(op, debilitated, "condition", "debilitado").Character;
        Check(D20Conditions.CannotAct(new(unconscious)), "repeated debilitated causes unconsciousness");
        Check(Apply(op, unconscious, "test", "per_reflexos").Rolls.Length == 0, "helpless automatically fails Reflexos");
        var fallen = Apply(t20, Apply(t20, Make(), "condition", "desprevenido").Character, "condition", "caido").Character;
        Check(D20Conditions.Defense(new(fallen), false, false) == 5 && D20Conditions.Defense(new(fallen), true, false) == 15, "T20 prone defense stacks with other conditions");
        var weakT20 = Apply(t20, Make(), "condition", "fraco").Character;
        weakT20 = Apply(t20, weakT20, "condition", "abalado").Character;
        Check(Apply(t20, weakT20, "test", "per_luta").Rolls[0].Expression == "1d20+3", "T20 applies minus two once, rather than subtracting dice");
        weakT20 = Apply(t20, weakT20, "condition", "fraco").Character;
        Check(Apply(t20, weakT20, "test", "per_luta").Rolls[0].Expression == "1d20+0", "T20 severe penalty minus five");
        var timed = Apply(op, Make(), "condition", "lento", 1).Character;
        timed = Apply(op, timed, "condition-persistent", "fraco").Character;
        Check(D20Conditions.Speed(new(timed)) == 4.5m, "movement rounds down to 1.5 m increments");
        timed = Apply(op, timed, "condition-round").Character;
        Check(!D20Conditions.Has(new(timed), "lento") && D20Conditions.Has(new(timed), "fraco"), "round expiration preserves persistent conditions");
        timed = Apply(op, timed, "scene").Character;
        Check(D20Conditions.Has(new(timed), "fraco"), "persistent negative duration survives scene transition");
        var overlapping = Apply(op, timed, "condition", "fraco", 1).Character;
        overlapping = Apply(op, overlapping, "condition-round").Character;
        Check(D20Conditions.Has(new(overlapping), "fraco") && !D20Conditions.Has(new(overlapping), "debilitado"), "persistent underlying weakness survives an expired stronger condition");
        var persistentSlow = Apply(op, Make(), "condition-persistent", "lento").Character;
        persistentSlow = Apply(op, persistentSlow, "condition", "lento", 1).Character;
        persistentSlow = Apply(op, persistentSlow, "condition-round").Character;
        Check(D20Conditions.Has(new(persistentSlow), "lento"), "reapplying a shorter condition never erases persistent duration");
        var fixedRounds = Apply(op, Make(), "condition", "lento", 3).Character;
        fixedRounds = Apply(op, fixedRounds, "scene").Character;
        Check(new RuleState(fixedRounds).Get("cond_lento") == 4, "scene change does not erase an explicitly timed condition");
        var broken = Apply(op, Make(), "condition", "alquebrado").Character;
        Check(Apply(op, broken, "spend", "normal", 2).Character.Mana == 7, "alquebrado increases PE costs");
        Check(Apply(t20, Make(), "spend-minimum", amount: 3).Character.Mana == 7, "minimum-cost exception may exceed level");
        Check(Apply(t20, Make(), "spend-general", amount: 2).Character.Mana == 8, "general ability uses character level");
        Reject(() => Apply(t20, Make(), "spend-general", amount: 3));
        var fire = Apply(op, Make(), "condition-persistent", "em_chamas").Character;
        Check(Apply(op, fire, "turn", die: 3).Character.Health == 17, "burning persists and damages on turn");
        var bleeding = Apply(op, Make(), "condition", "sangrando").Character;
        var stopped = Apply(op, bleeding, "turn", die: 20);
        Check(new RuleState(stopped.Character).Get("sangrando") == 0 && stopped.Rolls[0].Expression == "2d20kh1+0", "OP bleeding uses raw Vigor, not Fortitude bonus");
        var confused = Apply(op, Make(), "condition", "confuso").Character;
        var frozen = Apply(op, confused, "turn", die: 2).Character;
        Check(D20Conditions.CannotAct(new(frozen)), "confusion forbids actions for 2-3");
        Check(!D20Conditions.Has(new(Apply(op, frozen, "turn", die: 6).Character), "confuso"), "confusion six ends condition");
        var suffocating = Apply(op, Make(), "condition-persistent", "asfixiado").Character;
        suffocating = Apply(op, suffocating, "turn").Character; suffocating = Apply(op, suffocating, "turn").Character;
        Check(suffocating.Health == 20, "holding breath lasts Vigor rounds");
        suffocating = Apply(op, suffocating, "turn", die: 1).Character;
        Check(suffocating.Health == 19 && new RuleState(suffocating).Get("asfixia_inconsciente") == 1, "failed asphyxia save causes unconsciousness and loss");
        Check(new RuleState(Apply(op, suffocating, "breathe").Character).Get("asfixia_inconsciente") == 0, "breathing clears only its own unconsciousness source");
        var dying = Apply(op, Make(), "damage", amount: 20).Character;
        dying = Apply(op, dying, "stabilize", "medicine").Character;
        Check(SheetRuleActions.Describe(op, dying).Actions.Single(a => a.Id == "stabilize").Options![0].Label.Contains("25"), "repeat stabilization DC increases by five");
        var deaf = Apply(op, Make(), "condition", "surdo").Character;
        var failedRitual = Apply(op, deaf, "ritual", "other", 3, 1);
        Check(failedRitual.Character.Mana == 7 && failedRitual.Character.Sanity == 50 && failedRitual.Message.Contains("Ritual perdido"), "deafness requires concentration and failed casting spends PE without applying ritual SAN costs");
        var proneCaster = Apply(t20, Make(), "condition", "caido").Character;
        var lostSpell = Apply(t20, proneCaster, "cast", "general", 1, 1);
        Check(lostSpell.Character.Mana == 9 && lostSpell.Message.Contains("Magia perdida"), "poor casting conditions spend PM on failed concentration");
        var spell = Apply(t20, proneCaster, "cast", "general", 1, 20);
        Check(spell.Character.Mana == 9 && spell.Message.Contains("conjurada"), "natural twenty succeeds in T20 casting check");
        var armoredCaster = Apply(t20, Make(), "configure", "magia_arcana_armadura", 1).Character;
        var lostArcane = Apply(t20, armoredCaster, "cast-minimum", amount: 1, die: 1);
        Check(lostArcane.Character.Mana == 9 && lostArcane.Message.Contains("armadura"), "arcane armor check spends PM on failure");
        var racialSpell = Apply(t20, armoredCaster, "cast", "general", 1, 1);
        Check(racialSpell.Rolls.Length == 0 && racialSpell.Message.Contains("conjurada"), "racial/power casting is exempt from arcane armor check");
        var zero = new RuleState(weak); zero.Field("forca", 0);
        Reject(() => Apply(op, zero.Export(), "test", "per_luta"));
        zero.Set("dados_zero_penalidade", 4);
        Check(Apply(op, zero.Export(), "test", "per_luta").Rolls[0].Expression == "4d20kl1+5", "ambiguous zero-attribute penalty requires explicit table ruling");
        var invalid = new RuleState(Make()); invalid.Set("cond_fraco", -2);
        Check(SheetRuleActions.ValidateStateAccess(op, Make().Fields!, invalid.Export().Fields!, false) is not null, "imports reject invalid reserved condition durations");
        var custom = new RuleState(Make()); custom.Set("cond_autoral", 2);
        Check(SheetRuleActions.ValidateStateAccess(op, Make().Fields!, custom.Export().Fields!, false) is null, "existing custom state is preserved without assigning invented mechanics");
        Console.WriteLine("OP/T20 condition penalties, durations, turn effects, asphyxia and cost limits passed.");
    }
    private static SystemField N(string key, string? roll = null) => new(key, key, "number", "Testes", null, null, null, RollExpression: roll);
}
