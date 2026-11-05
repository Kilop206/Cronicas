using System.Text.Json;
using Chronicles.Api;

public static class CthulhuOccupationTests
{
    public static void Run()
    {
        SystemField Num(string key) => new(key, key, "number", "Criação", null, null, null);
        SystemField Table(string key, params (string, string)[] columns) => new(key, key, "table", "Criação", null, null, null, Columns: columns.Select(c => new SystemColumn(c.Item1, c.Item1, c.Item2)).ToArray());
        var skills = Enumerable.Range(1, 9).Select(i => new CthulhuSkillBase($"per_escolha_{i}", i == 1 ? "educacao" : i == 2 ? "floordiv(destreza, 2)" : "5")).ToArray();
        var slots = Enumerable.Range(1, 8).Select(i => new CthulhuOccupationSlot($"escolha_{i}", $"Escolha {i}", i == 1 ? ["per_escolha_1"] : null)).ToArray();
        var profile = new CthulhuOccupation("professor", "Professor", "educacao * 4", 20, 70, slots);
        var rules = new CthulhuOccupationDefinition(skills, [profile]);
        SystemField[] fields = [Table("estado_regras", ("chave","text"),("valor","number")), Table("criacao_pericias", ("chave","text"),("base","number"),("ocupacao","number"),("interesse","number")), Table("criacao_escolhas", ("slot","text"),("pericia","text")),
            .. new[]{"ocupacao","ocupacao_regra","ocupacao_versao"}.Select(key => new SystemField(key,key,"text","Criação",null,null,null)),
            Num("educacao"),Num("inteligencia"),Num("destreza"),Num("credito"), .. skills.Select(skill => Num(skill.Key))];
        var system = new SystemDefinition(fields, null, "call_of_cthulhu_7e", 6, "7e", CthulhuOccupations: rules);
        void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException("CoC occupation: " + description); }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected occupation rejection."); }
        Check(CthulhuOccupationDefinition.Validate(rules, fields) is null, "safe configurable definition");
        CharacterInput New()
        {
            var state = new RuleState(new("Novo",null,null,10,10,50,99,10,10)); state.Field("educacao",60); state.Field("inteligencia",70); state.Field("destreza",55); state.Set("criacao_fase",2); return state.Export();
        }
        CharacterInput Apply(CharacterInput c, string action, int amount = 0, string? option = null, string? expression = null, SystemDefinition? definition = null)
        {
            var sys = definition ?? system; var next = SheetRuleActions.Apply(sys,c,new(action,amount,option,Expression:expression),sides => 1).Character;
            Check(SheetRuleActions.ValidateStateAccess(sys,c.Fields!,next.Fields!,true,ruleExecution:true) is null,"every command satisfies server validation"); return next;
        }
        var blank = New(); var started = Apply(blank,"occupation-start",option:"professor"); var s = new RuleState(started);
        Check(s.N("per_escolha_1") == 60 && s.N("per_escolha_2") == 27 && s.Get("ocupacao_total") == 240 && s.Get("ocupacao_interesse_total") == 140,"base values, floor division and two distinct budgets");
        Check(SheetRuleActions.Describe(system,started).Occupation?.Skills.Length == 10,"native creation view includes Credit and real skill bases");
        Reject(() => Apply(started,"occupation-start",option:"professor"));
        Reject(() => Apply(started,"occupation-allocate",10,"per_escolha_2","occupation"));
        Reject(() => Apply(started,"occupation-allocate",1,"credito","interest"));
        Reject(() => Apply(started,"occupation-choice",option:"escolha_1",expression:"per_escolha_2"));
        var chosen = started;
        for (int i=1;i<=8;i++) chosen=Apply(chosen,"occupation-choice",option:$"escolha_{i}",expression:$"per_escolha_{i}");
        Reject(() => Apply(chosen,"occupation-choice",option:"escolha_2",expression:"per_escolha_1"));
        chosen=Apply(chosen,"occupation-allocate",50,"per_escolha_2","occupation");
        chosen=Apply(chosen,"occupation-allocate",10,"per_escolha_2","interest");
        Check(new RuleState(chosen).N("per_escolha_2") == 87,"base and both allocations combine in actual skill");
        Reject(() => Apply(chosen,"occupation-choice",option:"escolha_2",expression:"per_escolha_9"));
        Reject(() => Apply(chosen,"occupation-allocate",241,"per_escolha_1","occupation"));
        Reject(() => Apply(chosen,"occupation-allocate",141,"per_escolha_9","interest"));
        Reject(() => Apply(chosen,"occupation-allocate",-1,"per_escolha_9","interest"));
        Reject(() => Apply(chosen,"occupation-allocate",71,"credito","occupation"));
        Reject(() => Apply(chosen,"occupation-finish"));
        var refunded=Apply(chosen,"occupation-allocate",0,"per_escolha_2","occupation");
        refunded=Apply(refunded,"occupation-choice",option:"escolha_2",expression:"per_escolha_9");
        Check(new RuleState(refunded).N("per_escolha_2") == 37,"refund restores budget without discarding personal points");
        var forged = new RuleState(chosen); forged.Field("per_escolha_2",999);
        Check(SheetRuleActions.ValidateStateAccess(system,chosen.Fields!,forged.Export().Fields!,true) is not null,"ordinary autosave cannot bypass allocation even for master");
        forged=new RuleState(chosen); forged.Set("ocupacao_total",9999);
        Check(SheetRuleActions.ValidateStateAccess(system,chosen.Fields!,forged.Export().Fields!,false) is not null,"budgets cannot be forged via ordinary character update");
        var unrelated=new RuleState(chosen); unrelated.Field("idade",40);
        Check(SheetRuleActions.ValidateStateAccess(system,chosen.Fields!,unrelated.Export().Fields!,true) is null,"unrelated edits remain available");
        var malformed=new RuleState(chosen); malformed.Fields[CthulhuOccupationRules.Ledger]=JsonSerializer.SerializeToElement(new[]{new{chave="per_escolha_1"}});
        Check(SheetRuleActions.ValidateStateAccess(system,chosen.Fields!,malformed.Export().Fields!,true) is not null,"missing table cells are validation errors, not server errors");
        var credited=Apply(chosen,"occupation-allocate",20,"credito","occupation"); var finished=Apply(credited,"occupation-finish");
        Check(new RuleState(finished).Get("ocupacao_fase") == 2,"completion allows unused points to be discarded");
        Reject(() => Apply(finished,"occupation-reset")); Reject(() => Apply(finished,"occupation-allocate",1,"per_escolha_1","interest"));
        var developed=new RuleState(finished); developed.Field("per_escolha_2",100);
        Check(SheetRuleActions.ValidateStateAccess(system,finished.Fields!,developed.Export().Fields!,true) is null,"post-creation development is independent of archived allocation");
        var reset=Apply(chosen,"occupation-reset"); Check(new RuleState(reset).N("per_escolha_1") == 0 && new RuleState(reset).Get("criacao_fase") == 2,"reset restores pre-occupation skills without re-rolling characteristics");
        Apply(reset,"occupation-start",option:"professor");
        var old=new RuleState(blank); old.Field("per_legado",15); Reject(() => Apply(old.Export(),"occupation-start",option:"professor"));
        old=new RuleState(blank); old.Fields["per_escolha_1"]=JsonSerializer.SerializeToElement(0.5); Reject(() => Apply(old.Export(),"occupation-start",option:"professor"));
        Reject(() => Apply(new RuleState(new("Não gerado",null,null,10,10,50,99,10,10)).Export(),"occupation-start",option:"professor"));
        var changed=system with{CthulhuOccupations=rules with{SkillCap=75}};
        Check(SheetRuleActions.Describe(changed,chosen).Occupation?.Error is not null,"changed model is visibly detected");
        Reject(() => Apply(chosen,"occupation-allocate",1,"credito","occupation",changed));
        Apply(chosen,"occupation-reset",definition:changed);
        var personal=system with{CthulhuOccupations=rules with{PersonalCredit=true}};
        var flexible=Apply(blank,"occupation-start",option:"professor",definition:personal);
        flexible=Apply(flexible,"occupation-allocate",20,"credito","interest",personal);
        Check(new RuleState(flexible).N("credito") == 20,"personal Credit is an explicit editable table configuration");
        var capped=Apply(blank,"occupation-start",option:"professor",definition:changed);
        Reject(() => Apply(capped,"occupation-allocate",16,"per_escolha_1","interest",changed));
        Check(CthulhuOccupationDefinition.Validate(rules with{Skills=[skills[0] with{Formula="eval(1)"}]},fields) is not null,"arbitrary code rejected");
        Check(CthulhuOccupationDefinition.Validate(rules with{Skills=[skills[0] with{Formula="campo_ausente"},..skills.Skip(1)]},fields) is not null,"undefined formula references rejected");
        Check(CthulhuOccupationDefinition.Validate(rules with{Occupations=[profile with{Slots=[..slots.Take(7)]}]},fields) is not null,"exactly eight choices required");
        Check(CthulhuOccupationDefinition.Validate(rules with{Occupations=[profile with{CreditMin=80,CreditMax=70}]},fields) is not null,"invalid credit ranges rejected");
        Check(CthulhuOccupationDefinition.Validate(rules with{Occupations=[null!]},fields) is not null,"null JSON entries rejected");
        Check(CthulhuOccupationDefinition.Validate(rules with{Occupations=[profile with{Slots=[null!,..slots.Skip(1)]}]},fields) is not null,"null slots rejected");
        Check(CthulhuOccupationDefinition.Validate(rules with{Skills=[skills[0],skills[0]]},fields) is not null,"duplicate bases rejected");
        Check(CthulhuOccupationDefinition.Validate(rules,fields.Where(f=>f.Key!="criacao_escolhas").ToArray()) is not null,"missing creation storage rejected at model save");
        var copy=SystemDefinition.Read(JsonSerializer.Serialize(system)); Check(copy.CthulhuOccupations?.Occupations[0].Id == "professor","definitions round-trip in persisted system schema");
        Check(SheetRuleActions.Describe(system with{TemplateVersion=5},blank).Occupation is null,"old template versions retain their creation experience");
        Console.WriteLine("CoC occupation schemas, frozen bases, separate budgets, choices, Credit, refunds, completion, legacy protection and autosave validation passed.");
    }
}
