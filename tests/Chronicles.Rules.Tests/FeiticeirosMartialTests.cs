using System.Text.Json;

public static class FeiticeirosMartialTests
{
    public static void Run()
    {
        var engine=new FmRulesPlugin();
        var system=new SystemDefinition([new("estado_regras","Estado","table","Regras",null,null,null)],null,FmCatalog.Id,1,"2.5.2");
        RuleResult Apply(CharacterInput c,string action,int amount=0,string? option=null,int roll=10,FmActionContext? context=null)=>engine.Execute(system,c,new(action,amount,option,Jujutsu:context),sides=>Math.Min(sides,roll));
        CharacterInput Field(CharacterInput c,string key,object value)=>c with{Fields=new(c.Fields!){[key]=JsonSerializer.SerializeToElement(value)}};
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        int Get(CharacterInput c,string key)=>new RuleState(c).Get(key);
        void Check(bool condition,string label){if(!condition)throw new Exception("F&M martial: "+label);}
        void Reject(Action action,string label){try{action();}catch(ArgumentException){return;}throw new Exception("F&M martial accepted invalid: "+label);}
        CharacterInput New(string specialization="lutador",int level=10,string[]? maneuvers=null)
        {
            var values=new Dictionary<string,object>{["origem"]=specialization=="restringido"?"restringido":"inato",["especializacao"]=specialization,["atributo_tecnica"]="sabedoria",["atributo_especializacao"]="forca",["nivel"]=level,["forca"]=16,["destreza"]=14,["constituicao"]=14,["sabedoria"]=14,["inteligencia"]=16,["presenca"]=14,["natureza"]="humano",["defesa"]=16,["apt_controle"]=specialization=="restringido"?0:3,["aptidoes"]=specialization=="restringido"?Array.Empty<string>():new[]{"canalizar_golpe","canalizacao_avancada"},["manobras_lutador"]=maneuvers??[],["armas"]=new[]{new{nome="Teste",atributo="forca",dados="1d6",apos="",treinado=true,margem=20,acao="comum",municao=-1}}};
            var c=new CharacterInput("Marcial",null,null,1,1,0,0,0,0,values.ToDictionary(kv=>kv.Key,kv=>JsonSerializer.SerializeToElement(kv.Value)));
            return Apply(c,"fm-initialize").Character;
        }
        CharacterInput Next(CharacterInput c)=>Apply(Apply(c,"fm-turn-end").Character,"fm-turn-start").Character;
        var fighter=Apply(New(maneuvers:["ajuste","desarme","trabalho_pes"]),"fm-turn-start").Character;
        Check(Get(fighter,"empolgacao")==1,"starts combat at excitement 1 without first-turn decay");
        var hit=Apply(fighter,"fm-attack",option:"0",context:new(AttackKind:"martial"));
        Check(Get(hit.Character,"empolgacao_acerto")==1,"a successful attack marks this turn");
        var second=Next(hit.Character);
        Check(Get(second,"empolgacao")==2,"hit raises excitement at next turn, not immediately");
        var adjusted=Apply(second,"fm-attack",option:"0",context:new(AttackKind:"martial",FighterManeuver:"ajuste"));
        Check(adjusted.Rolls.Any(r=>r.Expression=="1d4")&&Get(adjusted.Character,"manobra_ajuste")==1,"adjustment rolls the current excitement die");
        Reject(()=>Apply(State(adjusted.Character,"acao_comum",1),"fm-attack",option:"0",context:new(AttackKind:"martial",FighterManeuver:"ajuste")),"maneuver once per round despite an extra action");
        Check(Get(Next(Next(adjusted.Character)),"empolgacao")==2,"a turn without a hit lowers excitement once");
        var maximum=Apply(State(fighter,"empolgacao",5),"fm-maneuver-hit").Character;
        Check(Get(Next(maximum),"empolgacao")==5,"excitement caps at five");
        var feet=Apply(State(fighter,"empolgacao",2),"fm-fighter-defense",option:"trabalho_pes").Character;
        Check(FmConditions.Defense(new(feet))==20&&Get(feet,"acao_bonus")==0,"footwork consumes bonus and applies rolled defense");
        Check(FmConditions.Defense(new(Next(feet)))==16,"footwork expires at next turn start");
        var advanced=Apply(State(Apply(New(level:11,maneuvers:["esquiva"]),"fm-turn-start").Character,"empolgacao",2),"fm-fighter-defense",option:"esquiva");
        Check(advanced.Rolls.Any(r=>r.Expression=="2d4")&&Get(advanced.Character,"acao_reacao")==0,"maximum excitement changes defensive dice and consumes reaction");
        Reject(()=>Apply(fighter,"fm-fighter-defense",option:"esquiva"),"unlearned maneuver");
        Reject(()=>Apply(Field(New(level:1),"manobras_lutador",new[]{"ajuste","desarme","esquiva"}),"fm-turn-start"),"learning budget follows actual class levels");
        var bonusAttack=Apply(hit.Character,"fm-attack",option:"0",context:new(AttackKind:"unarmed",UnarmedAction:"bonus"));
        Check(Get(bonusAttack.Character,"acao_bonus")==0&&bonusAttack.Rolls.Any(r=>r.Expression=="1d12"),"trained body unlocks bonus unarmed attack and class damage");
        Reject(()=>Apply(fighter,"fm-attack",option:"0",context:new(AttackKind:"unarmed",UnarmedAction:"bonus")),"bonus attack needs prior martial or unarmed attack");
        var superior=Apply(New(level:20),"fm-turn-start").Character;
        Check(Get(superior,"empolgacao")==2,"superior fighter starts at two");
        var free=Apply(superior,"fm-attack",option:"0",context:new(AttackKind:"unarmed",UnarmedAction:"superior"));
        Check(free.Character.Mana==superior.Mana-2&&Get(free.Character,"acao_comum")==1,"superior free attack pays PE without consuming common");
        Reject(()=>Apply(free.Character,"fm-attack",option:"0",context:new(AttackKind:"unarmed",UnarmedAction:"superior")),"superior free attack once per round");
        var combat=Apply(New("combate"),"fm-turn-start").Character;
        var strike=Apply(combat,"fm-attack",option:"0",context:new(AttackKind:"melee",CombatArt:"golpe_descendente"));
        Check(Get(strike.Character,"preparo_gasto")==1&&FmConditions.Defense(new(strike.Character))==17,"descending hit spends preparation and grants half wisdom defense");
        var thrown=Apply(strike.Character,"fm-attack",option:"0",context:new(AttackKind:"thrown",CombatArt:"arremesso_agil"));
        Check(Get(thrown.Character,"preparo_gasto")==2&&Get(thrown.Character,"acao_comum")==0,"agile throw follows melee as a free action");
        Reject(()=>Apply(thrown.Character,"fm-attack",option:"0",context:new(AttackKind:"thrown",CombatArt:"arremesso_agil")),"agile throw cannot chain from another throw");
        Reject(()=>Apply(combat,"fm-attack",option:"0",context:new(CombatArt:"execucao_silenciosa")),"silent execution requires declared offguard target");
        var silent=Apply(combat,"fm-attack",option:"0",context:new(CombatArt:"execucao_silenciosa",TargetOffguard:true));
        Check(silent.Rolls.Any(r=>r.Expression=="2d6"),"silent execution scales dice with wisdom");
        var miss=Apply(combat,"fm-attack",999,"0",context:new(AttackKind:"melee",CombatArt:"investida_imediata"));
        Check(Get(miss.Character,"preparo_gasto")==2&&miss.Message.Contains($"{3.0m} m"),"charge spends even on miss and reports explicit movement");
        var empty=State(combat,"preparo_gasto",12);
        Reject(()=>Apply(empty,"fm-attack",option:"0",context:new(AttackKind:"melee",CombatArt:"golpe_descendente")),"zero preparation is not restored by fallback");
        Check(Get(Apply(empty,"fm-preparation-recover",option:"analisar").Character,"preparo_gasto")==10,"analyzing restores two and consumes common action");
        var ended=Apply(empty,"fm-combat-end").Character;
        Check(Get(Apply(ended,"fm-short-rest").Character,"preparo_gasto")==6,"short rest restores half maximum preparation");
        Check(Get(Apply(ended,"fm-long-rest",option:"normal").Character,"preparo_gasto")==0,"long rest restores full preparation");
        var reaction=Apply(fighter,"fm-channel",1,"reacao").Character;
        Check(Get(reaction,"acao_reacao")==0&&Get(reaction,"acao_movimento")==1,"advanced channel uses reaction without movement");
        Reject(()=>Apply(Field(fighter,"aptidoes",new[]{"canalizar_golpe"}),"fm-channel",1,"reacao"),"base channel cannot use reaction");
        var restricted=Apply(New("restringido"),"fm-turn-start").Character;
        var sneak=Apply(restricted,"fm-attack",option:"0",context:new(AttackKind:"melee",SneakAttack:true,TargetFlanked:true));
        Check(sneak.Rolls.Any(r=>r.Expression=="4d8")&&Get(sneak.Character,"ataque_furtivo_usado")==1,"restricted sneak damage scales and consumes allowance on hit");
        Reject(()=>Apply(State(sneak.Character,"acao_comum",1),"fm-attack",option:"0",context:new(AttackKind:"melee",SneakAttack:true,TargetFlanked:true)),"sneak allowance prevents duplicate application");
        Console.WriteLine("F&M martial: excitement, learned maneuvers, defensive timing, body bonus/superior attacks, preparation arts and recovery, reaction channel and sneak attack passed.");
    }
}
