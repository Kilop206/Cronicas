using System.Text.Json;
using Chronicles.Api;
using static Chronicles.Api.CharacterEndpoints;

public static class CthulhuCreationTests
{
    public static void Run()
    {
        var keys = new[] { "forca", "constituicao", "tamanho", "destreza", "aparencia", "poder", "educacao", "inteligencia" };
        var stateField = new SystemField("estado_regras", "Estado", "table", "Regras", null, null, null);
        var system = new SystemDefinition([stateField], null, "call_of_cthulhu_7e", 5, "7e");
        CharacterInput New() => new("Novo investigador", null, null, 20, 20, 50, 99, 10, 20, keys.ToDictionary(k => k, _ => JsonSerializer.SerializeToElement(0)));
        RuleResult Apply(CharacterInput c, string action, int amount = 0, string? option = null) => SheetRuleActions.Apply(system, c, new(action, amount, option), sides => sides == 100 ? 100 : sides);
        void Check(bool test, string label) { if (!test) throw new InvalidOperationException("CoC creation: " + label); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected CoC creation rejection."); }
        var old = New(); old.Fields!["forca"] = JsonSerializer.SerializeToElement(50);
        Reject(() => Apply(old, "creation-generate", 40));
        var fractional = New(); fractional.Fields!["forca"] = JsonSerializer.SerializeToElement(0.5);
        Reject(() => Apply(fractional, "creation-generate", 40));
        var young = Apply(New(), "creation-generate", 18);
        Check(young.Rolls.Count(r => r.Expression == "3d6") == 7, "teenagers roll luck twice in addition to five characteristics");
        Check(new RuleState(young.Character).N("educacao") == 85 && new RuleState(young.Character).Get("criacao_reducao") == 5, "teen EDU reduction and shared STR/SIZ budget");
        Reject(() => Apply(young.Character, "creation-vitals"));
        Reject(() => Apply(young.Character, "creation-age-reduce", 5, "destreza"));
        var reduced = Apply(young.Character, "creation-age-reduce", 3, "forca").Character;
        reduced = Apply(reduced, "creation-age-reduce", 2, "tamanho").Character;
        Check(new RuleState(reduced).N("forca") == 87 && new RuleState(reduced).N("tamanho") == 88, "age reduction can be split without rounding to multiples of five");
        var finished = Apply(reduced, "creation-vitals").Character;
        Check(finished.MaxHealth == 17 && finished.MaxMana == 18 && finished.Sanity == 90 && finished.MaxSanity == 99, "creation applies rounded PV, POW/5 PM and initial SAN separately from SAN maximum");
        Reject(() => Apply(finished, "creation-generate", 18));
        Reject(() => Apply(finished, "creation-vitals"));
        var middle = Apply(New(), "creation-generate", 40);
        Check(new RuleState(middle.Character).N("educacao") == 99 && middle.Rolls.Count(r => r.Expression == "1d100") == 2, "education improvements are sequential and capped at ninety-nine");
        Check(new RuleState(middle.Character).N("aparencia") == 85 && new RuleState(middle.Character).Get("criacao_reducao") == 5, "forties creation applies its age bracket once");
        var elder = Apply(New(), "creation-generate", 85).Character;
        Check(new RuleState(elder).Get("criacao_reducao") == 80 && new RuleState(elder).N("aparencia") == 65, "eighties use non-cumulative creation adjustment");
        Reject(() => Apply(New(), "creation-generate", 12));
        var skill = New(); skill.Fields!["per_biblioteca"] = JsonSerializer.SerializeToElement(85);
        var marked = Apply(skill, "skill-mark", option: "per_biblioteca").Character;
        var developed = Apply(marked, "skill-develop", option: "per_biblioteca");
        Check(developed.Character.Fields!["per_biblioteca"].GetInt32() == 95 && developed.Character.Sanity == 62, "first mastery crossing awards two d6 SAN");
        marked = Apply(developed.Character, "skill-mark", option: "per_biblioteca").Character;
        var twice = Apply(marked, "skill-develop", option: "per_biblioteca");
        Check(twice.Character.Fields!["per_biblioteca"].GetInt32() == 105 && twice.Character.Sanity == 62, "skills may exceed one hundred without repeating mastery reward");
        var highCon = new RuleState(New()); highCon.Field("constituicao", 100); highCon.Set("morrendo", 1);
        Check(new RuleState(Apply(highCon.Export(), "dying-check").Character).Get("morto") == 1, "100 remains a fumble on CON tests even with characteristic 100");
        Check(CthulhuRules.Movement(50, 50, 50, 90) == 2 && CthulhuRules.Movement(50, 50, 50, 120) == 0, "movement continues to decrease in later decades and never becomes negative");
        Console.WriteLine("CoC random creation, age allocation, EDU, resource initialization and mastery development passed.");
    }
}
