using Chronicles.Api;
using Chronicles.Api.Data;
using Microsoft.EntityFrameworkCore;

public static class ParanormalThreatTests
{
    public static void Run()
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException("OP threats: "+label);}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Expected OP threat rejection.");}
        var basic=new ParanormalThreatSheet{Attributes=new(3,4,2,2,3),Skills=[new("fortitude","Fortitude","vigor",10),new("reflexos","Reflexos","agilidade",5),new("vontade","Vontade","presenca",10)]};
        ThreatResult Apply(ParanormalThreatSheet s,ThreatCommand c,int hp=30,Func<int,int>? draw=null)=>ParanormalThreatRules.Apply(s,hp,30,c,draw??(sides=>sides));
        Check(ParanormalThreatSchema.Validate(basic) is null,"blank native threat schema");
        var fractional=ParanormalThreatSchema.ToState(basic with{Speed=4.5m,State=new(){{"cond_lento",1}}},30,30);Check(D20Conditions.Speed(fractional)==1.5m,"fractional movement keeps half-speed rounding in 1.5m increments");
        var rolled=Apply(basic,new("check","fortitude",25));Check(rolled.Rolls[0].Expression=="3d20kh1+10"&&rolled.Rolls[0].Total==30,"uses final printed skill bonus without adding attribute again");
        var zero=Apply(basic with{Attributes=new(0,1,1,1,1)},new("check","agilidade"),draw:sides=>3);Check(zero.Rolls[0].Expression=="2d20kl1+0","zero attribute keeps the lowest die");
        var weapon=new ThreatAttack("garras","Ataque da mesa",Bonus:10,Damage:"1d6+3",ExtraDamage:"1d4",ExtraType:"sangue",Count:2,Ammunition:5);
        var attacker=basic with{Attacks=[weapon],Effort=5,MaxEffort=5};
        var attacks=Apply(attacker,new("attack","garras",25));Check(attacks.Attacks is {Length:2} && attacks.Attacks.All(a=>a.Critical&&a.Damage==15&&a.ExtraDamage==4),"each attack has its own critical, fixed modifier and undoubled extra damage");
        Check(attacks.Sheet.Attacks[0].Ammunition==3,"entire sequence consumes ammunition atomically");
        Reject(()=>Apply(attacker with{Attacks=[weapon with{Ammunition=1}]},new("attack","garras")));
        var low=Apply(attacker,new("attack","garras",10),draw:sides=>1);Check(low.Attacks!.All(a=>a.Hit==true),"OP natural one is not an automatic attack failure");
        var noCrit=Apply(attacker with{Attacks=[weapon with{Margin=18}]},new("attack","garras",100),draw:sides=>Math.Min(19,sides));Check(noCrit.Attacks!.All(a=>!a.Critical&&a.Hit==false),"expanded critical range still requires a hit");
        var cost=Apply(attacker with{Attacks=[weapon with{Cost=3}]},new("attack","garras"));Check(cost.Sheet.Effort==2,"cost belongs to the action rather than each attack");
        Reject(()=>Apply(attacker with{Effort=2,Attacks=[weapon with{Cost=3}]},new("attack","garras")));
        var feared=Apply(basic,new("condition","apavorado"));Check(feared.Sheet.State.Count==0,"paranormal origin blocks fear conditions");
        Check(Apply(basic,new("condition","atordoado")).Sheet.State.Count==0,"paranormal origin blocks mental stun");
        var human=basic with{Kind="human"};var stunned=Apply(human,new("condition","atordoado"));Reject(()=>Apply(stunned.Sheet with{Attacks=[weapon]},new("attack","garras")));
        var weakened=Apply(attacker,new("condition","fraco",1));var weakenedHit=Apply(weakened.Sheet,new("attack","garras"));Check(weakenedHit.Rolls[0].Expression.StartsWith("3d20kh1",StringComparison.Ordinal),"condition penalty removes an OP die from FOR 4");
        Check(Apply(weakened.Sheet,new("round")).Sheet.State.GetValueOrDefault("cond_fraco")==0,"round duration expires explicitly");
        var damage=Apply(basic,new("damage","mental",10));Check(damage.Health==30&&damage.Damage!.Immune,"mental damage immunity of paranormal creatures");
        Check(Apply(human,new("damage","mental",10)).Health==20,"mundane enemy loses HP to mental damage");
        Check(Apply(basic,new("damage","fogo",10,Source:"fear-ritual")).Health==30,"paranormal origin rejects fear rituals regardless of damage type");
        var protectedSheet=basic with{Defenses=[new("armor","all",Amount:2,SourceKind:"item",SourceId:"armor"),new("ritual1","fogo",Amount:5,SourceKind:"ritual",SourceId:"ritual1"),new("ritual2","fogo",Amount:3,SourceKind:"ritual",SourceId:"ritual2"),new("weak","fogo",Effect:"vulnerability")]};
        var reduced=Apply(protectedSheet,new("damage","fogo",15,Half:true));Check(reduced.Damage is {AfterSave:7,Multiplier:2,Reduction:7,Applied:7},"save before vulnerability before RD; item and ritual stack, rituals do not");
        Check(Apply(protectedSheet,new("damage","fogo",15,IgnoreResistance:true)).Damage!.Applied==30,"explicit RD bypass does not bypass vulnerability");
        var abilities=basic with{Defenses=[new("a","all",Amount:3,SourceKind:"ability",SourceId:"first"),new("b","fogo",Amount:5,SourceKind:"ability",SourceId:"first"),new("c","all",Amount:2,SourceKind:"ability",SourceId:"second")]};
        Check(Apply(abilities,new("damage","fogo",10)).Damage!.Reduction==7,"same ability selects maximum, distinct abilities stack");
        var exception=basic with{Defenses=[new("except","all",Amount:10,Except:["morte"])]};Check(Apply(exception,new("damage","morte",12)).Health==18&&Apply(exception,new("damage","corte",12)).Health==28,"explicit RD bypass type");
        var enigma=basic with{Defenses=[new("invulnerable","all",Effect:"immunity",DisabledByEnigma:true)]};Check(Apply(enigma,new("damage","corte",9)).Health==30,"unsolved enigma defense");
        var solved=Apply(enigma,new("enigma",Amount:1));Check(Apply(solved.Sheet,new("damage","corte",9)).Health==21,"only configured defenses disable after enigma");
        Reject(()=>Apply(basic with{CriticalImmune=true},new("damage","corte",20,Critical:true)));
        Check(Apply(basic with{CriticalImmune=true},new("damage","corte",20,Critical:true,NormalAmount:8)).Health==22,"critical immunity uses explicitly supplied normal damage");
        var regen=basic with{FastHealing=new(5,["quimico"])};var blocked=Apply(regen,new("damage","quimico",6));var ordinary=Apply(blocked.Sheet,new("damage","corte",10),blocked.Health);
        var recovery=Apply(ordinary.Sheet,new("turn"),ordinary.Health);Check(recovery.Health==19&&recovery.Sheet.State["cura_impedida"]==6,"fast healing tracks wounds it cannot heal");
        var healed=Apply(recovery.Sheet,new("heal",Amount:30),recovery.Health);Check(healed.Health==30&&healed.Sheet.State.GetValueOrDefault("cura_impedida")==0,"normal healing clears blocked wounds");
        Check(Apply(regen,new("turn"),0).Health==0,"fast healing does not presume revival");
        Check(Apply(protectedSheet,new("damage","fogo",7,Half:true,Source:"hunger")).Damage is {Applied:7,Reduction:0,Multiplier:1},"direct HP loss bypasses saves, vulnerability and damage defenses");
        var power=human with{Abilities=[new("power","Poder da mesa",Cost:2,Roll:"1d6+{presenca}",MaximumUses:1,RemainingUses:1,Recharge:"scene")],Effort=5,MaxEffort=5};
        var used=Apply(power,new("ability","power"));Check(used.Sheet.Effort==3&&used.Sheet.Abilities[0].RemainingUses==0&&used.Rolls[0].Total==6,"ability pays cost, uses and records bounded dice");Reject(()=>Apply(used.Sheet,new("ability","power")));
        Check(Apply(used.Sheet,new("scene")).Sheet.Abilities[0].RemainingUses==1,"scene recharge");
        var paralyzed=power with{State=new(){{"cond_paralisado",-1}}};Reject(()=>Apply(paralyzed,new("ability","power")));
        Check(Apply(paralyzed with{Abilities=[paralyzed.Abilities[0] with{MentalOnly=true}]},new("ability","power")).Sheet.Effort==3,"paralysis allows an explicitly pure mental action while blocking physical abilities");
        Check(Apply(basic with{PresenceTargets=[Guid.NewGuid()]},new("scene")).Sheet.PresenceTargets.Length==0,"presence exposure ledger resets only on new scene");
        Check(ParanormalThreatSchema.Validate(basic with{Attacks=[weapon with{Damage="1d6; eval()"}]}) is not null,"unsafe expression rejected");
        Check(ParanormalThreatSchema.Validate(basic with{Skills=[null!]}) is not null,"null nested components rejected");
        Check(ParanormalThreatSchema.Validate(basic with{Attributes=null!}) is not null,"null attributes rejected");
        Check(ParanormalThreatSchema.Validate(basic with{State=new(){{"cond_atordoado",1}}}) is not null,"import cannot store an immune mental condition");
        Check(ParanormalThreatSchema.Validate(basic with{Version=2}) is not null,"unknown sheet version is not silently interpreted");
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unconnected_model_test").Options);
        Check(!db.Database.HasPendingModelChanges(),"migration snapshot matches the application model");
        Console.WriteLine("OP threat schemas, final bonuses, multiattacks, ammunition, costs, conditions, source-aware RD, enigma, fast healing, abilities and migration model passed.");
    }
}
