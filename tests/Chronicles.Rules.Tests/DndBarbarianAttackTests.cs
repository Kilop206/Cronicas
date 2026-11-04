using System.Text.Json;
using Chronicles.Api;
using static Chronicles.Api.CharacterEndpoints;

public static class DndBarbarianAttackTests
{
    public static void Run()
    {
        var system=new SystemDefinition([
            new("estado_regras","Estado","table","Regras",null,null,null),new("defesas_dano","Defesas","table","Defesas",null,null,null),new("ataques_regras","Armas","table","Ataques",null,null,null),
            new("mod_forca","FOR","number","Atributos",null,null,null),new("mod_destreza","DES","number","Atributos",null,null,null)],null,"dnd_5e",5,"2014");
        CharacterInput New(int level=9,string type="corpo",string ability="mod_forca",string damage="2d6+3",string extra="1d4")
        {
            var s=new RuleState(new("Bárbaro",null,null,30,30,0,0,0,0));s.Set("nivel_barbaro",level);s.Field("mod_forca",3);s.Field("mod_destreza",3);
            s.Fields[WeaponActions.FieldKey]=JsonSerializer.SerializeToElement(new[]{new{nome="Arma",tipo=type,@base=ability,dano=damage,dano_extra=extra,municao=-1}});return s.Export();
        }
        RuleResult Attack(CharacterInput c,WeaponContext? weapon=null,Func<int,int>? draw=null,SystemDefinition? definition=null)=>SheetRuleActions.Apply(definition??system,c,new("attack",Option:"0",Weapon:weapon),draw??(sides=>sides));
        CharacterInput Command(CharacterInput c,string action)=>SheetRuleActions.Apply(system,c,new(action),sides=>sides).Character;
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        void Check(bool value,string label){if(!value)throw new InvalidOperationException("D&D barbarian attacks: "+label);}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Expected barbarian attack rejection.");}
        var first=Attack(New(),new(Reckless:true));
        Check(first.Rolls[0].Expression.StartsWith("2d20kh1",StringComparison.Ordinal),"first STR melee attack gains advantage");
        Check(first.Rolls.Select(r=>r.Expression).SequenceEqual(new[]{"2d20kh1+3","4d6","2d4","1d6"}),"brutal critical adds one weapon die, not all 2d6, and never doubles extra again");
        Check(first.Message.Contains("dano 41",StringComparison.Ordinal)&&first.Message.Contains("Crítico Brutal +1d6",StringComparison.Ordinal),"critical result includes original modifier and extra damage once");
        Check(new RuleState(first.Character).Get("descuidado_exposto")==1,"incoming advantage persists in saved state");
        Reject(()=>Attack(first.Character,new(Reckless:true)));
        Check(Attack(first.Character).Rolls[0].Expression.StartsWith("2d20kh1",StringComparison.Ordinal),"subsequent melee STR attacks retain advantage");
        Check(Attack(first.Character,new(OutsideTurn:true)).Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"reactions outside turn do not gain reckless advantage");
        var ended=Command(first.Character,"combat-turn-end");Check(new RuleState(ended).Get("descuidado_exposto")==1&&!Dnd2014BarbarianAttacks.Reckless(new(ended)),"end turn removes offensive benefit while preserving exposure");
        var next=Command(ended,"combat-turn-start");Check(new RuleState(next).Get("descuidado_exposto")==0&&new RuleState(next).Get("ataques_turno")==0,"next turn ends exposure and resets attack count");
        Check(Attack(next,new(Reckless:true)).Rolls[0].Expression.StartsWith("2d20kh1",StringComparison.Ordinal),"choice is available on next turn");
        Reject(()=>Attack(New(1),new(Reckless:true)));Reject(()=>Attack(New(),new(Reckless:true,OutsideTurn:true)));
        var regular=Attack(New(),draw:sides=>1);Reject(()=>Attack(regular.Character,new(Reckless:true)));
        Check(Attack(New(),new(Reckless:true),sides=>1).Character.Fields![WeaponActions.FieldKey].ValueKind==JsonValueKind.Array,"miss still persists the first attack and reckless choice");
        Check(Attack(State(New(),"cond_envenenado",1),new(Reckless:true)).Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"condition disadvantage cancels reckless advantage");
        Check(Attack(State(New(),"modo_ataque",-1),new(Reckless:true)).Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"manual disadvantage also cancels advantage");
        Check(Attack(New(9,ability:"mod_destreza"),new(Reckless:true)).Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"DEX melee cannot benefit from reckless");
        Check(Attack(New(9,ability:"mod_destreza")).Message.Contains("Crítico Brutal",StringComparison.Ordinal),"brutal critical does not require STR or rage");
        Check(!Attack(New(9,type:"distancia"),new(Reckless:true)).Message.Contains("Crítico Brutal",StringComparison.Ordinal),"ranged or thrown attack has no brutal critical");
        foreach(var (level,count) in new[]{(8,0),(9,1),(12,1),(13,2),(16,2),(17,3),(20,3)})
        {
            var result=Attack(New(level,damage:"1d12+3",extra:"0"));
            Check(count>0 ? result.Rolls[^1].Expression==$"{count}d12" && result.Rolls.Length==3 : result.Rolls.Length==2,$"brutal tier {level}");
        }
        var noncritical=Attack(New(17),draw:sides=>Math.Min(sides,10));Check(noncritical.Rolls.Length==3&&!noncritical.Message.Contains("Crítico Brutal",StringComparison.Ordinal),"ordinary hits do not roll brutal dice");
        Reject(()=>Attack(New(damage:"1d6+1d4+3")));Reject(()=>Attack(New(),new(BrutalDie:12)));Reject(()=>Attack(New(),new(BrutalDie:7)));
        Check(Attack(New(damage:"1d6+1d4+3"),new(BrutalDie:6)).Rolls[^1].Expression=="1d6","mixed base dice require an explicit matching weapon die");
        Check(!Attack(New(damage:"3",extra:"1d6")).Message.Contains("Crítico Brutal",StringComparison.Ordinal),"flat damage does not invent a weapon die from extra damage");
        var outside=Attack(New(),new(OutsideTurn:true)).Character;Check(new RuleState(outside).Get("ataques_turno")==0,"reaction before own turn does not consume first-attack choice");
        var confirmed=Command(New(),"rage-hostile-attack");Reject(()=>Attack(confirmed,new(Reckless:true)));
        var old=Attack(New(),new(Reckless:true),definition:system with{TemplateVersion=4});Check(old.Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal)&&!old.Message.Contains("Crítico Brutal",StringComparison.Ordinal),"legacy template does not silently activate new mechanics");
        Reject(()=>Attack(State(New(),"cond_incapacitado",1),new(Reckless:true)));
        Check(Dnd2014BarbarianAttacks.Validate(new(State(New(),"descuidado_turno",2))) is not null,"malformed state is rejected on autosave");
        Console.WriteLine("D&D reckless attack, turn/reaction context, exposure, advantage cancellation, brutal critical dice and legacy compatibility passed.");
    }
}
