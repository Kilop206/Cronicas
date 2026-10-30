using System.Text.Json;
using Chronicles.Api;
using static Chronicles.Api.CharacterEndpoints;

public static class SheetRuleActionTests
{
    public static void Run()
    {
        var stateField = new SystemField("estado_regras", "Estado", "table", "Regras", null, null, null, Columns: [new("chave", "Chave", "text"), new("valor", "Valor", "number")]);
        SystemDefinition System(string id, int version, string edition) => new([stateField], null, id, version, edition);
        var op = System("ordem_paranormal", 6, "1.3"); var dnd = System("dnd_5e", 3, "2014");
        var coc = System("call_of_cthulhu_7e", 4, "7e"); var t20 = System("tormenta20", 3, "jogo-do-ano-2024-v1.3");
        CharacterInput Make(params (string, object)[] values) => new("Teste", null, null, 20, 30, 50, 99, 10, 20, values.ToDictionary(v => v.Item1, v => JsonSerializer.SerializeToElement(v.Item2)));
        CharacterInput Configure(CharacterInput c, params (string, int)[] values) { var s = new RuleState(c); foreach (var (key, value) in values) s.Set(key, value); return s.Export(); }
        RuleResult Apply(SystemDefinition sys, CharacterInput c, string action, int amount = 0, string? option = null, int die = 10) => SheetRuleActions.Apply(sys, c, new(action, amount, option), sides => Math.Min(sides, die));
        void Check(bool test, string label) { if (!test) throw new InvalidOperationException("Rule actions: " + label); }
        void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejection: " + label); }

        Check(!SheetRuleActions.Supports(dnd with { RulesetVersion = "2024" }), "2014 engine rejects 2024");
        Check(!SheetRuleActions.Supports(op with { TemplateVersion = 5 }), "old template requires explicit upgrade");
        var agent = Make(("nex", 35));
        var spent = Apply(op, agent, "spend", 6, "normal");
        Check(spent.Character.Mana == 4 && new RuleState(spent.Character).Get("pe_turno") == 6, "PE budget persisted");
        Reject(() => Apply(op, spent.Character, "spend", 2, "normal"), "PE turn cap");
        var minimum = Apply(op, Make(("nex", 5)), "spend", 2, "minimum");
        Check(minimum.Character.Mana == 8, "minimum-cost exception at NEX5");
        Reject(() => Apply(op, minimum.Character, "spend", 1, "minimum"), "minimum exception does not stack");
        var wounded = Apply(op, agent, "damage", 20).Character;
        wounded = Apply(op, wounded, "heal", 1).Character;
        Check(new RuleState(wounded).Get("morrendo") == 1 && wounded.Health == 1, "healing does not remove dying in OP");
        wounded = Apply(op, wounded, "turn").Character;
        wounded = Apply(op, wounded, "stabilize", option: "medicine").Character;
        wounded = Apply(op, wounded, "damage", 1).Character;
        wounded = Apply(op, wounded, "turn").Character;
        wounded = Apply(op, wounded, "turn").Character;
        Check(new RuleState(wounded).Get("morto") == 1, "OP three nonconsecutive dying turns");
        var sleep = Apply(op, agent, "sleep", option: "comfortable").Character;
        Check(sleep.Health == 30 && sleep.Mana == 20, "OP sleep clamps recovery");

        var hero = Make(("pontos_temporarios", 7), ("mod_constituicao", 2));
        var hit = Apply(dnd, hero, "damage", 10, "normal").Character;
        Check(hit.Health == 17 && hit.Fields!["pontos_temporarios"].GetInt32() == 0, "temp HP first");
        Check(Apply(dnd, hero, "temporary", 3).Character.Fields!["pontos_temporarios"].GetInt32() == 7, "temporary HP do not stack");
        var unconscious = hero with { Health = 0 };
        var critical = Apply(dnd, unconscious, "damage", 1, "critical").Character;
        Check(new RuleState(critical).N("salvaguardas_falha") == 2, "damage at zero causes death failures even with temporary HP");
        var bare = Make() with { Health = 0 };
        var death = Apply(dnd, bare, "death-save", die: 1).Character;
        Check(death.Fields!["salvaguardas_falha"].GetInt32() == 2, "natural 1 causes two failures");
        var twenty = Apply(dnd, death, "death-save", die: 20).Character;
        Check(twenty.Health == 1 && twenty.Fields!["salvaguardas_falha"].GetInt32() == 0, "natural 20 restores 1 HP");
        var instant = Apply(dnd, Make(), "damage", 50, "normal").Character;
        Check(new RuleState(instant).Get("morto") == 1, "massive excess damage");
        Reject(() => Apply(dnd, instant, "heal", 5), "ordinary healing does not revive");
        var paladin = Configure(hero, ("nivel_paladino", 5), ("nivel_guerreiro", 2));
        Check(Dnd2014Rules.SpellSlots(new RuleState(paladin)).SequenceEqual(new[] { 4, 2 }), "noncaster multiclass retains paladin table");
        var multiclass = Configure(hero, ("nivel_paladino", 5), ("nivel_mago", 2));
        Check(Dnd2014Rules.SpellSlots(new RuleState(multiclass)).SequenceEqual(new[] { 4, 3 }), "multiclass floors half caster");
        var slot = Apply(dnd, paladin, "cast", 2).Character;
        Check(new RuleState(slot).Get("slot_usado_2") == 1, "spell slot consumed");
        var rest = Apply(dnd, slot, "long-rest", option: "confirmed").Character;
        Check(rest.Health == rest.MaxHealth && new RuleState(rest).Get("slot_usado_2") == 0, "long rest resets slots");

        var investigator = Make(("forca", 75), ("destreza", 60), ("tamanho", 50), ("idade", 50), ("constituicao", 60), ("inteligencia", 70), ("per_biblioteca", 70), ("sorte", 50));
        Check(CthulhuRules.Movement(75, 60, 50, 50) == 7, "CoC age-based movement");
        Check(CthulhuRules.Movement(50, 50, 50, 30) == 8, "equal characteristics do not grant MOV 9");
        var major = Apply(coc, investigator, "damage", 15, die: 90).Character;
        Check(new RuleState(major).Get("ferimento_grave") == 1 && major.Health == 5, "major wound at half max HP");
        Check(new RuleState(Apply(coc, investigator, "damage", 31).Character).Get("morto") == 1, "single hit greater than max HP kills in core rules");
        Check(new RuleState(Apply(coc, investigator, "damage", 30).Character).Get("morto") == 0, "core rules distinguish exactly maximum damage from instant death");
        var mythos = Configure(investigator, ("mitos", 60));
        Check(Apply(coc, mythos, "sanity-heal", 50).Character.Sanity == 39, "SAN capped by Mythos");
        var marked = Apply(coc, investigator, "skill-mark", option: "per_biblioteca").Character;
        var improved = Apply(coc, marked, "skill-develop", option: "per_biblioteca", die: 100).Character;
        Check(improved.Fields!["per_biblioteca"].GetInt32() == 80 && new RuleState(improved).Get("dev_per_biblioteca") == 0, "marked skill development and unmark");
        Reject(() => Apply(coc, improved, "skill-develop", option: "per_biblioteca"), "no double development");
        var loss = Apply(coc, investigator, "sanity-loss", 10, die: 1).Character;
        Check(new RuleState(loss).Get("insanidade_temporaria") == 1 && new RuleState(loss).Get("insanidade_indefinida") == 1, "SAN five points and one-fifth day thresholds");

        var adventurer = Make(("constituicao", 3), ("nivel", 4), ("forca", 2));
        adventurer = Configure(adventurer, ("nivel_guerreiro", 3), ("nivel_paladino", 1));
        var basic = Apply(t20, adventurer, "baseline", option: "guerreiro").Character;
        Check(basic.MaxHealth == 47 && basic.MaxMana == 12, "T20 first-class HP and subsequent multiclass levels");
        var negative = Apply(t20, adventurer, "damage", 25).Character;
        Check(new RuleState(negative).Get("pv_negativos") == 5, "T20 preserves negative HP");
        negative = Apply(t20, negative, "heal", 7).Character;
        Check(negative.Health == 2 && new RuleState(negative).Get("pv_negativos") == 0, "T20 healing pays negative HP first");
        Reject(() => Apply(t20, adventurer, "spend", 2, "paladino"), "T20 PM cap uses class level");
        var bleed = Apply(t20, adventurer, "damage", 25).Character;
        bleed = Apply(t20, bleed, "turn", die: 1).Character;
        Check(new RuleState(bleed).Get("pv_negativos") == 6, "T20 failed CON applies bleeding loss");
        bleed = Apply(t20, bleed, "heal", 1).Character;
        Check(new RuleState(bleed).Get("sangrando") == 0 && new RuleState(bleed).Get("pv_negativos") == 5, "one point healing stabilizes even with negative HP");
        var caster = Make(("nex", 35), ("intelecto", 2), ("per_ocultismo", 5));
        var ritual = Apply(op, caster, "ritual", 3, "other", die: 1).Character;
        Check(ritual.Mana == 7 && ritual.Sanity == 47 && ritual.MaxSanity == 98, "OP ritual severe failure applies permanent sanity cost");
        var before = Make().Fields!;
        var optionalLuck = Configure(Make(), ("sorte_opcional", 1)).Fields!;
        Check(SheetRuleActions.ValidateStateAccess(coc, before, optionalLuck, false) is not null, "player cannot activate optional rules via normal save/import/restore");
        Check(SheetRuleActions.ValidateStateAccess(coc, before, optionalLuck, true) is null, "GM may activate optional rules");
        Console.WriteLine("Edition rule actions: damage, death, rest, slots, costs, wounds, sanity, development and multiclass passed.");
    }
}
