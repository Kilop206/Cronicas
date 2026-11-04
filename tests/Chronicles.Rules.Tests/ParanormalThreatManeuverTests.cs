using Chronicles.Api;

public static class ParanormalThreatManeuverTests
{
    public static void Run()
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException("OP maneuvers: "+label);}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Expected invalid maneuver rejection.");}
        var input=new ThreatManeuverInput(1,"trip",Guid.NewGuid(),"character",1,InReach:true);
        var sheet=new ParanormalThreatSheet{Kind="human",ManeuverProfile=new(true,"forca",5),Attributes=new(3,3,2,1,2)};
        ManeuverCombatant Fighter(int bonus,Func<int,int>? draw=null,string size="medium",bool includesSize=true,Dictionary<string,int>? conditions=null)=>ParanormalThreatManeuvers.Threat(sheet with{Size=size,ManeuverProfile=new(true,"forca",bonus,includesSize),State=conditions??[]},30,30,draw??(s=>s));
        var a=Fighter(5);var d=Fighter(1);var close=ParanormalThreatManeuvers.Resolve(a,d,input);
        Check(close is {Outcome:"success",ProneApplied:true,Displacement:0}&&d.State.Get("cond_caido")==1,"trip applies actual scene-duration prone without extra displacement below five points");
        Check(a.State.Health==30&&d.State.Health==30,"ordinary trip never infers automatic damage");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5),Fighter(0),input) is {Displacement:1.5m,ProneApplied:true},"trip at a five-point margin adds one square");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(15),Fighter(0),input).Displacement==1.5m,"trip additional displacement does not scale like push");
        var push=input with{Kind="push"};
        foreach(var (bonus,expected) in new[]{(1,1.5m),(4,1.5m),(5,3m),(9,3m),(10,4.5m)})
        {var defender=Fighter(0);var result=ParanormalThreatManeuvers.Resolve(Fighter(bonus),defender,push);Check(result.Displacement==expected&&!result.ProneApplied&&defender.State.State.Count==0,"push displacement threshold "+bonus);}
        var loss=ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(5),input);Check(loss is {Outcome:"failed",ProneApplied:false,Displacement:0},"defender win creates no condition or displacement");
        var natural=ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(30,_=>1),input);Check(natural.Outcome=="failed","opposed test compares totals instead of substituting a fixed-DC automatic hit");
        int at=0;int[] values=[10,10,15,5];int Draw(int _)=>values[at++];
        var tieA=ParanormalThreatManeuvers.Threat(sheet with{Attributes=new(1,1,1,1,1),ManeuverProfile=new(true,"forca",0)},30,30,Draw);
        var tieD=ParanormalThreatManeuvers.Threat(sheet with{Attributes=new(1,1,1,1,1),ManeuverProfile=new(true,"forca",0)},30,30,Draw);
        Check(ParanormalThreatManeuvers.Resolve(tieA,tieD,push) is {Ties:1,Displacement:4.5m,Rounds.Length:2},"tie repeats both participants and retains the final margin");
        var tied=ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(0),input);Check(tied is {Outcome:"tied",Ties:20,ProneApplied:false,Rounds.Length:20},"bounded repeated ties remain unresolved rather than inventing a winner");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5,size:"tiny",includesSize:false),Fighter(0),push) is {Outcome:"tied"},"tiny size subtracts five when not already included");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5,size:"tiny"),Fighter(0),push).Displacement==3,"printed final modifier is not charged size twice");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(0,size:"colossal",includesSize:false),Fighter(0),push).Displacement==4.5m,"colossal maneuver modifier adds ten");
        foreach(var (size,expected) in new[]{("tiny",-5),("small",-2),("medium",0),("large",2),("huge",5),("colossal",10)})Check(ParanormalThreatManeuvers.SizeModifier(size)==expected,"size category "+size);
        var weakened=Fighter(5,conditions:new(){{"cond_fraco",1}});ParanormalThreatManeuvers.Resolve(weakened,Fighter(0),input);Check(weakened.State.Rolls[0].Expression=="2d20kh1+5","physical condition reduces attack pool without changing final bonus");
        var fallen=Fighter(5,conditions:new(){{"cond_caido",1}});ParanormalThreatManeuvers.Resolve(fallen,Fighter(0),input);Check(fallen.State.Rolls[0].Expression=="1d20+5","prone attack penalty applies to the melee maneuver");
        Reject(()=>ParanormalThreatManeuvers.Resolve(Fighter(0,conditions:new(){{"cond_atordoado",1}}),Fighter(0),input));
        Reject(()=>ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(0,conditions:new(){{"cond_inconsciente",1}}),input));
        Reject(()=>ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(0),input with{InReach=false}));
        Reject(()=>ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(0),input with{Kind="invented"}));
        Reject(()=>ParanormalThreatManeuvers.Resolve(Fighter(0),Fighter(0),input with{AttackerAdjustment=1000}));
        Reject(()=>ParanormalThreatManeuvers.Threat(sheet with{ManeuverProfile=new()},30,30,_=>20));
        var immune=Fighter(0) with{Immune=true};var blocked=ParanormalThreatManeuvers.Resolve(Fighter(5),immune,input);Check(blocked.Outcome=="blocked"&&immune.State.Rolls.Count==0,"general maneuver immunity blocks before rolling");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5),Fighter(0) with{ProneImmune=true},input).Outcome=="blocked","prone immunity blocks trip");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5),Fighter(0) with{ProneImmune=true},push).Outcome=="success","condition immunity alone does not grant immunity to push");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5),Fighter(0),input,sourceBlocked:true).Rounds.Length==0,"blocked incorporeal/fear source never rolls");
        Check(ParanormalThreatManeuvers.Resolve(Fighter(5),Fighter(0),input with{TargetImmune=true}).Rounds.Length==0,"explicit GM exception is honored by pure rule engine");
        int Miss(int sides)=>sides==10?2:sides;
        var camo=ParanormalThreatManeuvers.Resolve(Fighter(5,Miss),Fighter(0),input,20);Check(camo is {Outcome:"concealment",ConcealmentRoll:2,ProneApplied:false},"winning opposition can still fail from actual concealment die");
        var ghost=ParanormalThreatManeuvers.Threat(sheet with{Incorporeal=true,Attributes=new(4,0,1,1,1)},30,30,_=>20);Check(ghost.Attribute=="agilidade","incorporeal maneuver uses agility rather than an absent strength check");
        var record=new ThreatManeuverRecord(Guid.NewGuid(),input.TargetId,"character","Alvo autoral","trip","success",25,20,0,1.5m,true,DateTime.UtcNow);
        Check(ParanormalThreatSchema.Validate(sheet with{ManeuverLog=[record]}) is null,"actual maneuver receipt validates");
        Check(ParanormalThreatSchema.Validate(sheet with{ManeuverLog=Enumerable.Repeat(record,11).ToArray()}) is not null,"receipt history bounded to ten entries");
        Check(ParanormalThreatSchema.Validate(sheet with{ManeuverProfile=null!}) is not null,"null maneuver profile rejected");
        Check(ParanormalThreatSchema.Validate(sheet with{ManeuverLog=[null!]}) is not null,"null receipt rejected");
        Console.WriteLine("OP maneuvers: opposed Luta, repeated ties, size/final bonuses, trip/push margins, conditions, immunity, concealment, incorporeal attribute and bounded private receipts passed.");
    }
}
