using System.Text.Json;
using Chronicles.Api;

public static class DndDamageRageTests
{
    public static void Run()
    {
        SystemField Number(string key,string? formula=null) => new(key,key,"number","Combate",null,null,null,Formula:formula,RollExpression:"1d20+{"+key+"}");
        var state=new SystemField("estado_regras","Estado","table","Regras",null,null,null);
        var defenses=new SystemField("defesas_dano","Defesas","table","Defesas",null,null,null);
        var weapons=new SystemField("ataques_regras","Ataques","table","Regras",null,null,null);
        var system=new SystemDefinition([state,defenses,weapons,Number("mod_forca"),Number("mod_destreza"),Number("res_forca"),Number("res_constituicao"),Number("per_atletismo","mod_forca + proficiencia"),Number("res_destreza")],null,"dnd_5e",5,"2014");
        void Check(bool test,string label){if(!test)throw new InvalidOperationException("D&D damage/rage: "+label);}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Expected D&D combat rejection.");}
        CharacterInput New(int level=1,int hp=20)
        {
            var s=new RuleState(new("Bárbaro",null,null,hp,20,0,0,0,0));s.Set("nivel_barbaro",level);s.Field("mod_forca",3);s.Field("mod_destreza",3);s.Field("res_forca",5);s.Field("res_constituicao",3);s.Field("res_destreza",3);s.Field("per_atletismo",5);return s.Export();
        }
        CharacterInput Field(CharacterInput c,string key,object value){c=c with{Fields=new(c.Fields!)};c.Fields![key]=JsonSerializer.SerializeToElement(value);return c;}
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        RuleResult Apply(CharacterInput c,string action,int amount=0,string? option=null,DamageEvent? damage=null,Func<int,int>? draw=null,string? expression=null,SystemDefinition? definition=null)
        {
            var sys=definition??system;var result=SheetRuleActions.Apply(sys,c,new(action,amount,option,Expression:expression,Damage:damage),draw??(sides=>sides));
            Check(SheetRuleActions.ValidateStateAccess(sys,c.Fields!,result.Character.Fields!,true,ruleExecution:true) is null,"command output satisfies server state validation");return result;
        }
        CharacterInput Defense(CharacterInput c,params (string Type,string Effect,string Condition)[] rows)=>Field(c,"defesas_dano",rows.Select(r=>new{tipo=r.Type,efeito=r.Effect,condicao=r.Condition,fonte="Fonte de teste"}));
        RuleResult Hit(CharacterInput c,params DamagePart[] parts)=>Apply(c,"damage-typed",damage:new(parts,Attack:true));
        var resistant=Defense(New(),("fire","resistance","always"),("all","resistance","nonmagical"));
        var simple=Hit(resistant,new DamagePart("fire",5));Check(simple.Damage?.Total==2&&simple.Character.Health==18,"duplicate resistances halve once and round down");
        var combined=Hit(resistant,new DamagePart("fire",1),new DamagePart("fire",1,Magical:true));Check(combined.Damage?.Total==1&&combined.Damage.Groups.Length==1,"same type and applicable defenses combine before rounding regardless of irrelevant flags");
        var both=Hit(Defense(New(),("fire","resistance","always"),("fire","vulnerability","always")),new DamagePart("fire",3));Check(both.Damage?.Total==2,"resistance is applied before vulnerability with integer rounding");
        var immune=Hit(Defense(New(),("poison","immunity","always"),("poison","vulnerability","always")),new DamagePart("poison",10));Check(immune.Damage?.Total==0&&immune.Character.Health==20&&immune.Rolls.Length==0,"immunity prevents damage despite vulnerability");
        var conditional=Defense(New(),("slashing","resistance","nonmagical_nonsilvered_attacks"));
        Check(Hit(conditional,new DamagePart("slashing",7)).Damage?.Total==3,"conditional physical resistance");
        Check(Hit(conditional,new DamagePart("slashing",7,Silvered:true)).Damage?.Total==7,"silvered bypass is explicit");
        Check(Hit(conditional,new DamagePart("slashing",7,Magical:true)).Damage?.Total==7,"magical bypass is explicit");
        Check(Apply(conditional,"damage-typed",damage:new([new DamagePart("slashing",7)])).Damage?.Total==7,"attack-only resistance does not reduce falling or other non-attack damage");
        Check(Hit(Defense(New(),("piercing","immunity","nonmagical_nonadamantine_attacks")),new DamagePart("piercing",7,Adamantine:true)).Damage?.Total==7,"adamantine bypass is explicit");
        var petrified=Hit(State(New(),"cond_petrificado",1),new DamagePart("fire",5),new DamagePart("poison",9));Check(petrified.Damage?.Total==2,"petrification grants resistance to all damage and poison immunity");
        var concentrating=State(Field(resistant,"pontos_temporarios",4),"concentrando",1);
        var mixed=Hit(concentrating,new DamagePart("fire",10,Magical:true),new DamagePart("cold",4,Magical:true));
        Check(mixed.Damage?.Total==9&&mixed.Character.Health==15&&mixed.Character.Fields!["pontos_temporarios"].GetInt32()==0&&mixed.Rolls.Length==1,"one source applies temporary HP once and makes one concentration save");
        var zero=Hit(New(hp:0),new DamagePart("fire",2),new DamagePart("cold",2));Check(zero.Character.Fields!["salvaguardas_falha"].GetInt32()==1,"compound hit at zero HP adds one death failure");
        var crit=Apply(New(hp:0),"damage-typed",damage:new([new DamagePart("fire",2)],Critical:true,Attack:true));Check(crit.Character.Fields!["salvaguardas_falha"].GetInt32()==2,"critical marker affects death saves without doubling supplied damage");
        Check(new RuleState(Hit(New(),new DamagePart("force",40)).Character).Get("morto")==1,"massive damage is tested after typed defenses");
        Reject(()=>Hit(New(),new DamagePart("bad_type",1)));Reject(()=>Hit(New(),new DamagePart("fire",-1)));Reject(()=>Hit(New(),new DamagePart("fire",10000)));Reject(()=>Hit(New(),[..Enumerable.Repeat(new DamagePart("fire",1),13)]));Reject(()=>Apply(New(),"damage-typed"));
        var partial=Field(New(),"defesas_dano",new[]{new{tipo="fire",efeito="",condicao="",fonte="Em edição"}});
        Check(Dnd2014Damage.Validate(system,new(partial)) is null,"partial defense editing can autosave");Reject(()=>Hit(partial,new DamagePart("fire",10)));
        var invalid=Defense(New(),("fire","arbitrary_code","always"));Check(Dnd2014Damage.Validate(system,new(invalid)) is not null,"unknown defense effects rejected");
        Check(Dnd2014Damage.Validate(system,new(Field(New(),"pontos_temporarios",-1))) is not null,"negative temporary HP cannot increase incoming damage");
        Reject(()=>Hit(New(),new DamagePart("fire",0)));
        Reject(()=>Apply(New(),"damage-typed",damage:new([new DamagePart("fire",2)],Critical:true)));
        var raging=Apply(State(New(),"concentrando",1),"rage-start",option:"ready").Character;
        Check(new RuleState(raging).Get("concentrando")==0&&new RuleState(raging).Get("classe_usado_furia")==1,"rage consumes its resource and ends concentration");
        Reject(()=>Apply(raging,"rage-start",option:"ready"));Reject(()=>Apply(raging,"cast",1));Reject(()=>Apply(raging,"pact-cast"));Reject(()=>Apply(raging,"concentrate"));
        Check(Hit(raging,new DamagePart("slashing",9,Magical:true)).Damage?.Total==4,"rage physical resistance includes magical damage");
        Check(Hit(raging,new DamagePart("psychic",9)).Damage?.Total==9,"rage does not invent mental resistance");
        Check(Apply(raging,"test",option:"res_forca").Rolls[0].Expression.StartsWith("2d20kh1",StringComparison.Ordinal),"rage grants strength save advantage");
        Check(Apply(raging,"test",option:"per_atletismo").Rolls[0].Expression.StartsWith("2d20kh1",StringComparison.Ordinal),"strength-derived skills gain advantage");
        Check(Apply(State(raging,"cond_envenenado",1),"test",option:"per_atletismo").Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"strength advantage cancels disadvantage without stacking");
        Check(Apply(raging,"test",option:"res_destreza").Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"other saves remain unchanged");
        object[] Weapon(string type,string key="mod_forca")=>[new{nome="Machado",@base=key,tipo=type,dano="1d6+3",dano_extra="0",municao=-1,treinado=false,bonus=0}];
        var attack=Apply(Field(raging,"ataques_regras",Weapon("corpo")),"attack",option:"0",expression:"hostile");
        Check(attack.Message.Contains("dano 17",StringComparison.Ordinal)&&attack.Message.Contains("+2",StringComparison.Ordinal),"rage damage is a flat bonus and is not doubled on critical");
        Check(Apply(Field(raging,"ataques_regras",Weapon("distancia")),"attack",option:"0").Message.Contains("dano 15",StringComparison.Ordinal),"thrown/ranged strength attacks do not gain rage damage");
        Check(Apply(Field(raging,"ataques_regras",Weapon("corpo","mod_destreza")),"attack",option:"0").Message.Contains("dano 15",StringComparison.Ordinal),"dexterity melee attacks do not gain rage damage");
        var missed=Apply(Field(raging,"ataques_regras",Weapon("corpo")),"attack",option:"0",expression:"hostile",draw:sides=>1);
        Check(new RuleState(Apply(missed.Character,"combat-turn-end").Character).Get("furia_rodadas")==9,"missing a hostile creature still maintains rage");
        Check(new RuleState(Apply(raging,"combat-turn-end").Character).Get("furia_rodadas")==0,"rage ends without attack or damage");
        var before=Apply(New(),"rage-hostile-attack").Character;before=Apply(before,"rage-start",option:"ready").Character;
        Check(new RuleState(Apply(before,"combat-turn-end").Character).Get("furia_rodadas")==9,"an attack earlier in the activation turn is remembered");
        var heavy=Apply(New(),"rage-start",option:"heavy").Character;
        Check(Hit(heavy,new DamagePart("slashing",8)).Damage?.Total==8&&Apply(heavy,"test",option:"res_forca").Rolls[0].Expression.StartsWith("1d20",StringComparison.Ordinal),"heavy armor suspends benefits");Reject(()=>Apply(heavy,"cast",1));
        heavy=Apply(heavy,"rage-armor",option:"ready").Character;Check(Hit(heavy,new DamagePart("slashing",8)).Damage?.Total==4,"armor changes update rage benefits");
        var persistent=Apply(New(15),"rage-start",option:"ready").Character;
        persistent=Apply(persistent,"combat-extra-turn-end").Character;Check(new RuleState(persistent).Get("furia_rodadas")==10,"extra turns do not advance the duration clock");
        for(int i=0;i<9;i++)persistent=Apply(persistent,"combat-turn-end").Character;
        Check(new RuleState(persistent).Get("furia_rodadas")==1,"persistent rage ignores activity but still has finite duration");persistent=Apply(persistent,"combat-turn-end").Character;Check(!Dnd2014Rage.Active(new(persistent)),"rage expires after ten confirmed turns");
        var ended=Apply(raging,"condition",option:"inconsciente").Character;Check(!Dnd2014Rage.Active(new(ended)),"unconsciousness ends rage immediately");
        var relentless=Apply(New(11,hp:5),"rage-start",option:"ready-relentless").Character;
        var saved=Hit(relentless,new DamagePart("force",6));Check(saved.Character.Health==1&&new RuleState(saved.Character).Get("furia_implacavel_usos")==1&&Dnd2014Rage.Active(new(saved.Character)),"relentless rage makes its opted-in CON save and preserves rage at one HP");
        var failed=Apply(saved.Character,"damage-typed",damage:new([new DamagePart("force",2)]),draw:sides=>1);Check(failed.Character.Health==0&&failed.Message.Contains("CD 15",StringComparison.Ordinal)&&!Dnd2014Rage.Active(new(failed.Character)),"relentless DC escalates and failure ends rage");
        var rested=Apply(failed.Character with{Health=5},"short-rest").Character;Check(new RuleState(rested).Get("furia_implacavel_usos")==0,"short rest resets relentless difficulty");
        var killed=Hit(relentless,new DamagePart("force",30));Check(new RuleState(killed.Character).Get("morto")==1&&killed.Rolls.Length==0,"instant death does not trigger relentless rage");
        var infinite=Apply(New(20),"rage-start",option:"ready").Character;Check(new RuleState(infinite).Get("classe_usado_furia")==0,"barbarian twenty has unlimited activations");
        Reject(()=>Apply(New(),"damage-typed",damage:new([new DamagePart("fire",1)]),definition:system with{TemplateVersion=4}));
        Reject(()=>Apply(New(),"rage-start",option:"ready",definition:system with{RulesetVersion="2024"}));
        Console.WriteLine("D&D typed damage, defense conditions, one-hit concentration, death saves, rage benefits, armor, duration and relentless rage passed.");
    }
}
