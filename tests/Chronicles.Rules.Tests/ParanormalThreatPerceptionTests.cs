using Chronicles.Api;

public static class ParanormalThreatPerceptionTests
{
    public static void Run()
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException("OP perception: "+label);}
        void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new InvalidOperationException("Expected invalid sensory context rejection.");}
        var context=new ThreatSensoryContext(Active:true,Distance:9,Concealment:2,Cause:"invisibility",OpponentUnseen:true);
        var ordinary=new ParanormalThreatSheet{Kind="human",Defense=20,Attributes=new(3,3,2,2,2),Skills=[new("reflexos","Reflexos","agilidade",5),new("percepcao","Percepção","presenca",5)],Attacks=[new("teste","Ataque autoral",Bonus:10)],SensoryContext=context};
        ThreatPerceptionView View(ParanormalThreatSheet sheet)=>ParanormalThreatPerception.Resolve(sheet,ParanormalThreatSchema.ToState(sheet,30,30));
        ThreatResult Apply(ParanormalThreatSheet sheet,ThreatCommand command,Func<int,int>? draw=null)=>ParanormalThreatRules.Apply(sheet,30,30,command,draw??(sides=>sides));
        var normalView=View(ordinary);Check(normalView is {MissChance:50,PerceivesOpponent:false},"normal senses cannot perceive invisible opponent");
        var scent=ordinary with{SensoryAbilities=new(Scent:true)};
        Check(View(scent) is {MissChance:20,PerceivesOpponent:true,VisualCheckAvailable:false},"scent at nine meters reduces total concealment but is not vision");
        Check(View(scent with{SensoryContext=context with{Distance=9.01m}}).MissChance==50,"scent does not extend past short range");
        Check(View(scent with{SensoryContext=context with{Distance=0}}).MissChance==20,"explicit zero-distance opponent is in range");
        Check(View(scent with{SensoryContext=context with{Distance=null}}) is {MissChance:50,Warning:not null},"unknown distance never silently grants a ranged benefit");
        Check(View(scent with{SensoryContext=context with{Unavailable=true}}).MissChance==50,"GM can declare special sense unavailable");
        var blindsight=ordinary with{SensoryAbilities=new(BlindsightRange:9)};
        Check(View(blindsight) is {MissChance:0,PerceivesOpponent:true,VisualCheckAvailable:false},"blindsight perceives invisibility without inventing visual details");
        Check(View(blindsight with{SensoryContext=context with{Distance=9.1m}}).MissChance==50,"blindsight range respected");
        Check(View(blindsight with{SensoryAbilities=new(BlindsightRange:4.5m),SensoryContext=context with{Distance=4.5m}}).MissChance==0,"custom fractional blindsight range");
        Check(View(blindsight with{SensoryContext=context with{Cause="paranormal-darkness"}}).MissChance==0,"blindsight is unaffected by paranormal darkness");
        Check(View(blindsight with{SensoryContext=context with{Cause="other"}}).MissChance==50,"blindsight does not infer how an arbitrary obstruction affects its sense");
        var dark=ordinary with{SensoryAbilities=new(Darkvision:true),SensoryContext=context with{Distance=18,Cause="darkness"}};
        Check(View(dark) is {MissChance:0,VisualCheckAvailable:true},"darkvision ignores normal darkness through eighteen meters");
        Check(View(dark with{SensoryContext=dark.SensoryContext with{Distance=18.01m}}).MissChance==50,"darkvision ends at medium range");
        Check(View(dark with{SensoryContext=dark.SensoryContext with{Cause="paranormal-darkness"}}).MissChance==50,"darkvision cannot bypass paranormal darkness");
        Check(View(dark with{SensoryContext=dark.SensoryContext with{Cause="invisibility"}}).MissChance==50,"darkvision does not reveal invisible opponents");
        var low=ordinary with{SensoryAbilities=new(LowLightVision:true),SensoryContext=new(Active:true,Distance:9,Concealment:1,Cause:"darkness")};
        Check(View(low).MissChance==0,"low-light vision ignores light concealment due to ordinary darkness");
        Check(View(low with{SensoryContext=low.SensoryContext with{Concealment=2}}).MissChance==50,"low-light ability does not silently upgrade to darkvision");
        Check(View(low with{SensoryContext=low.SensoryContext with{Cause="other"}}).MissChance==20,"low-light vision does not ignore foliage or fog");
        Check(View(low with{SensoryContext=low.SensoryContext with{Distance=9.1m}}).MissChance==20,"low-light vision range respected");
        var physicallyBlind=dark with{State=new(){{"cond_cego",1}}};Check(View(physicallyBlind).MissChance==50,"darkvision does not cure physical blindness");
        var blindScent=scent with{State=new(){{"cond_cego",1}}};var state=ParanormalThreatSchema.ToState(blindScent,30,30);ParanormalThreatPerception.Prepare(blindScent,state);
        Check(D20Conditions.Has(state,"cego")&&D20Conditions.Has(state,"lento")&&!D20Conditions.Has(state,"desprevenido")&&D20Conditions.Defense(state,false,true)==20,"scent suppresses only blindness's unperceived-opponent relation, preserving physical condition");
        var grabbed=ParanormalThreatSchema.ToState(blindScent with{State=new(){{"cond_cego",1},{"cond_agarrado",1}}},30,30);ParanormalThreatPerception.Prepare(blindScent,grabbed);Check(D20Conditions.Has(grabbed,"desprevenido"),"scent never removes a different source of unpreparedness");
        Check(Apply(ordinary,new("check","reflexos")).Rolls[0].Expression=="2d20kh1+5","opponent-specific unpreparedness applies to the check");
        Check(Apply(scent,new("check","reflexos")).Rolls[0].Expression=="3d20kh1+5","scent avoids that opponent-specific Reflex penalty");
        var checkedBlind=Apply(blindScent,new("check","reflexos"));Check(checkedBlind.Sheet.State.Count==1&&checkedBlind.Sheet.State.ContainsKey("cond_cego"),"contextual conditions and relation exemptions are never exported into persistent condition state");
        var visionOnly=scent with{State=new(){{"sentido_percepcao",1}}};Check(Apply(visionOnly,new("check","percepcao")).Rolls.Length==0,"scent is not accepted for a specifically visual task");
        Check(Apply(scent,new("check","percepcao")).Rolls.Length>0,"general perception can use the configured alternate sense");
        Check(Apply(blindsight with{State=new(){{"sentido_percepcao",2},{"cond_surdo",1}}},new("check","percepcao")).Rolls.Length==0,"blindsight does not arbitrarily cure deafness or identify sound");
        var missed=Apply(scent,new("attack","teste",10),sides=>sides==10?2:sides);Check(missed.Attacks is [{Hit:false,MissChance:20,ConcealmentRoll:2}],"scent's twenty percent chance has an actual inclusive failure threshold");
        var hit=Apply(scent,new("attack","teste",10),sides=>sides==10?3:sides);Check(hit.Attacks is [{Hit:true,MissChance:20,ConcealmentRoll:3}],"third d10 face succeeds against twenty percent chance");
        Check(Apply(blindsight,new("attack","teste",10)).Attacks is [{Hit:true,MissChance:0,ConcealmentRoll:null}],"no pointless miss-chance die after eligible blindsight");
        Check(Apply(ordinary with{State=new(){{"ignora_camuflagem",1}}},new("attack","teste",10)).Perception is {MissChance:0,PerceivesOpponent:false},"ignoring concealment alone does not grant perception or remove unpreparedness");
        var saved=Apply(ordinary,new("perception-context",PerceptionContext:context with{Distance=4.5m}));Check(saved.Sheet.SensoryContext.Distance==4.5m,"context command persists fractional distance");
        Reject(()=>Apply(ordinary,new("perception-context")));
        foreach(var invalid in new[]{context with{Distance=-1},context with{Distance=1000},context with{Concealment=3},context with{Cause="guess"}})Reject(()=>Apply(ordinary,new("perception-context",PerceptionContext:invalid)));
        Check(ParanormalThreatSchema.Validate(ordinary with{SensoryAbilities=null!}) is not null,"null sensory ability rejected");
        Check(ParanormalThreatSchema.Validate(ordinary with{SensoryContext=null!}) is not null,"null sensory context rejected");
        Check(ParanormalThreatSchema.Validate(ordinary with{SensoryAbilities=new(BlindsightRange:-1)}) is not null,"negative sense range rejected");
        var cleared=Apply(scent,new("scene"));Check(!cleared.Sheet.SensoryContext.Active&&cleared.Perception!.MissChance==0,"new scene clears the target context and returns its actual current projection");
        var legacy=ordinary with{SensoryContext=new(),State=new(){{"camuflagem_alvo",2}}};Check(View(legacy).MissChance==50,"inactive additive context preserves legacy concealment behavior");
        Check(Apply(legacy,new("check","reflexos")).Rolls[0].Expression=="3d20kh1+5","legacy target concealment does not retroactively create contextual conditions");
        Console.WriteLine("OP perception: scent, blindsight, low-light/darkvision, ranges, visibility origins, contextual defenses/checks, physical-condition preservation, actual concealment dice and legacy compatibility passed.");
    }
}
