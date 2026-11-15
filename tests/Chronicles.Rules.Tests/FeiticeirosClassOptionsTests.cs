using System.Text.Json;

public static class FeiticeirosClassOptionsTests
{
    public static void Run()
    {
        var engine=new FmRulesPlugin();
        var system=new SystemDefinition([new("estado_regras","Estado","table","Regras",null,null,null)],null,FmCatalog.Id,1,"2.5.2");
        RuleResult Apply(CharacterInput c,string action,int amount=0,string? option=null,int roll=10,FmActionContext? context=null,Func<int,int>? draw=null)=>engine.Execute(system,c,new(action,amount,option,Jujutsu:context),draw??(sides=>Math.Min(sides,roll)));
        CharacterInput Field(CharacterInput c,string key,object value)=>c with{Fields=new(c.Fields!){[key]=JsonSerializer.SerializeToElement(value)}};
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        int Get(CharacterInput c,string key)=>new RuleState(c).Get(key);
        void Check(bool condition,string label){if(!condition)throw new Exception("F&M class options: "+label);}
        void Reject(Action action,string label){try{action();}catch(ArgumentException){return;}throw new Exception("F&M class options accepted invalid: "+label);}
        CharacterInput New(string specialization="combate",int level=8,string[]? choices=null)
        {
            var values=new Dictionary<string,object>{["origem"]="inato",["especializacao"]=specialization,["atributo_tecnica"]="sabedoria",["atributo_especializacao"]=specialization=="tecnica"?"sabedoria":"forca",["nivel"]=level,["forca"]=16,["destreza"]=14,["constituicao"]=14,["sabedoria"]=14,["inteligencia"]=16,["presenca"]=14,["natureza"]="humano",["defesa"]=16,["aptidoes"]=choices??[],
                ["pericias"]=new[]{new{chave="prestidigitacao",treino=1,outros=0},new{chave="feiticaria",treino=2,outros=0}},
                ["armas"]=new[]{new{nome="Ensaio",atributo="forca",dados="1d6",apos="",treinado=true,margem=20,acao="comum",municao=-1}},
                ["feiticos"]=new[]{new{nome="Feitiço de ensaio",nivel=1,tipo="ataque",acao="comum",duracao="imediata",dados="4d8",reducao=0}}};
            return Apply(new("Opções",null,null,1,1,0,0,0,0,values.ToDictionary(kv=>kv.Key,kv=>JsonSerializer.SerializeToElement(kv.Value))),"fm-initialize").Character;
        }
        CharacterInput Spell(CharacterInput c,string type="ataque",string action="comum",int level=1,string dice="4d8")=>Field(c,"feiticos",new[]{new{nome="Feitiço",nivel=level,tipo=type,acao=action,duracao="imediata",dados=dice,reducao=0}});
        CharacterInput Next(CharacterInput c)=>Apply(Apply(c,"fm-turn-end").Character,"fm-turn-start").Character;
        var defensive=New(choices:["estilo_defensivo"]);
        Check(FmConditions.Defense(new(defensive))==20,"defensive style scales at 4 and 8");
        var duelist=Apply(New(choices:["estilo_duelista"]),"fm-attack",option:"0",context:new(AttackKind:"melee",SingleWeaponFreeHand:true));
        Check(duelist.Rolls[0].Total==22&&duelist.Message.Contains("13 de dano"),"duelist applies declared hand context and scaled attack/damage");
        var noContext=Apply(New(choices:["estilo_duelista"]),"fm-attack",option:"0");
        Check(noContext.Rolls[0].Total==20&&noContext.Message.Contains("9 de dano"),"style is not guessed from weapon name");
        Reject(()=>Apply(New(choices:["estilo_duelista"]),"fm-attack",option:"0",context:new(SingleWeaponFreeHand:true,DualWielding:true)),"incompatible hand declarations");
        var ranged=Apply(New(choices:["estilo_distante"]),"fm-attack",option:"0",context:new(AttackKind:"ranged"));
        Check(ranged.Rolls[0].Total==22&&ranged.Message.Contains("13 de dano"),"distant style");
        Check(Apply(New(choices:["estilo_arremessador"]),"fm-attack",option:"0",context:new(AttackKind:"thrown")).Message.Contains("13 de dano"),"thrown style");
        Check(Apply(New(level:1,choices:["estilo_duplo"]),"fm-attack",option:"0",context:new(AttackKind:"offhand",OffhandLight:true)).Message.Contains("10 de dano"),"dual style restores offhand attribute and adds its own damage");
        var massive=Field(New(level:1,choices:["estilo_massivo"]),"armas",new[]{new{nome="Pesada",atributo="forca",dados="2d6+2",apos="",treinado=true,margem=20,acao="comum",municao=-1}});
        var queue=new Queue<int>([10,1,2,1,1]);
        var rerolled=Apply(massive,"fm-attack",option:"0",context:new(AttackKind:"melee",WeaponHeavy:true),draw:_=>queue.Dequeue());
        Check(queue.Count==0&&rerolled.Message.Contains("8 de dano"),"massive rerolls once and accepts worse new values without repeating constants");
        var interceptor=Apply(Apply(New(choices:["estilo_interceptador"]),"fm-turn-start").Character,"fm-style-intercept",option:"forca");
        Check(interceptor.Message.Contains("33")&&Get(interceptor.Character,"acao_reacao")==0,"interceptor scales pool and spends reaction");
        var protector=Apply(Apply(New(choices:["estilo_protetor"]),"fm-turn-start").Character,"fm-style-protect",option:"resistencia").Character;
        Reject(()=>Apply(protector,"fm-style-protect",option:"ataque"),"protector does not grant a second reaction");
        Reject(()=>New(level:5,choices:["estilo_defensivo","estilo_duplo"]),"second style requires real level six");
        Reject(()=>New("tecnica",choices:["estilo_defensivo"]),"other classes do not gain a free combat style");

        var strike=Apply(New(),"fm-attack",option:"0",roll:19,context:new(AttackKind:"melee",SpecialStrike:["letal","atroz"]));
        Check(strike.Message.Contains("crítico")&&strike.Message.Contains("27 de dano")&&strike.Character.Mana==29,"lethal margin, extra weapon die and three PE cost");
        Check(!Apply(New(),"fm-attack",999,"0",roll:19,context:new(SpecialStrike:["letal"])).Message.Contains("crítico"),"expanded critical margin still needs a hit");
        var turn=Apply(New(),"fm-turn-start").Character;
        var precise=Apply(turn,"fm-attack",option:"0",context:new(SpecialStrike:["preciso"])).Character;
        precise=Apply(State(precise,"acao_comum",1),"fm-attack",option:"0",context:new(SpecialStrike:["preciso"])).Character;
        Check(precise.Mana==29,"precise cost rises from one to two PE within a round");
        Check(Apply(Next(precise),"fm-attack",option:"0",context:new(SpecialStrike:["preciso"])).Character.Mana==28,"precise cost resets on turn start");
        var slow=Apply(turn,"fm-attack",option:"0",context:new(SpecialStrike:["lento","desfocado","desfocado","desfocado"]));
        Check(slow.Character.Mana==31&&slow.Rolls[0].Total==8&&Get(slow.Character,"acao_comum")==0&&Get(slow.Character,"acao_bonus")==0&&Get(slow.Character,"acao_movimento")==1,"slow/confused modifiers enforce full action, stacked penalty and floor one");
        Reject(()=>Apply(New(),"fm-attack",option:"0",context:new(SpecialStrike:["desfocado","desfocado","desfocado","desfocado"])),"confused repetition cap");
        Reject(()=>Apply(New(),"fm-attack",option:"0",context:new(SpecialStrike:["sacrificio"])),"sacrifice cannot discount before damage is confirmed");
        Reject(()=>Apply(New(level:3),"fm-attack",option:"0",context:new(SpecialStrike:["atroz"])),"special strike actual class prerequisite");
        var bleeding=Apply(New(),"fm-attack",option:"0",context:new(SpecialStrike:["sanguinario","sanguinario","impactante","penetrante"]));
        Check(bleeding.Message.Contains("médio, CD 23")&&bleeding.Message.Contains("ignore 4 de RD"),"target instructions use specialization DC and character level");
        var sufficient=Apply(New(level:20),"fm-attack",option:"0",context:new(SpecialStrike:["atroz"]));
        Check(sufficient.Character.Mana==80&&sufficient.Message.Contains("21 de dano"),"self sufficient temporary points expire locally and class extra die applies");
        var boost=Apply(New(level:20),"fm-attack",option:"0",context:new(SpecialStrike:["amplo","letal","penetrante"],BoostSelfSufficient:true));
        Check(boost.Character.Mana==80&&Get(boost.Character,"autossuficiente_reforco")==1,"six temporary points once per scene");
        Reject(()=>Apply(boost.Character,"fm-attack",option:"0",context:new(SpecialStrike:["atroz"],BoostSelfSufficient:true)),"self sufficient scene limit");
        var blood=Apply(State(New() with{Mana=5},"preparo_gasto",2),"fm-attack",option:"0",roll:20,context:new(TargetEnemy:true)).Character;
        Check(blood.Mana==6,"critical enemy attack renews one PE");
        blood=Apply(blood,"fm-target-defeated").Character;
        Check(blood.Mana==6&&Get(blood,"preparo_gasto")==1,"kill confirmation restores preparation without double rewarding the critical");
        Reject(()=>Apply(blood,"fm-target-defeated"),"defeat receipt is single use");
        var normalHit=Apply(New() with{Mana=5},"fm-attack",option:"0",context:new(TargetEnemy:true)).Character;
        Check(Apply(normalHit,"fm-target-defeated").Character.Mana==6,"noncritical enemy defeat renews energy");
        Check(Apply(New() with{Mana=5},"fm-attack",option:"0",roll:20).Character.Mana==5,"renewal requires declared enemy");

        var defenses=Field(New(),"defesas",new[]{new{tipo="impacto",efeito="resistencia",valor=0,fonte="Origem"},new{tipo="impacto",efeito="rd",valor=6,fonte="Armadura"}});
        Check(Apply(defenses,"fm-damage",20,"impacto").Character.Health==defenses.Health-4,"ordinary resistance precedes RD");
        Check(Apply(defenses,"fm-damage",20,"impacto",context:new(IgnoreReduction:4)).Character.Health==defenses.Health-8,"penetrating reduces effective RD rather than bypassing resistance");
        Check(Apply(defenses,"fm-damage",20,"impacto",context:new(IgnoreReduction:9999,IgnoreResistance:true)).Character.Health==defenses.Health-20,"black flash incoming context ignores resistance and RD");
        var immune=Field(defenses,"defesas",new[]{new{tipo="impacto",efeito="imunidade",valor=0,fonte="Origem"}});
        Check(Apply(immune,"fm-damage",20,"impacto",context:new(IgnoreReduction:9999,IgnoreResistance:true)).Character.Health==immune.Health,"ignoring resistance never ignores immunity");
        Reject(()=>Apply(defenses,"fm-damage",20,"impacto",context:new(IgnoreReduction:-1)),"negative ignored RD");

        var cruel=Apply(Spell(New("tecnica",choices:["fundamento_cruel"]),"resistencia"),"fm-cast",option:"0",context:new(Fundamentals:["cruel","cruel"]));
        Check(cruel.Character.Mana==46&&cruel.Message.Contains("CD 23"),"cruel spends one PE per step and raises saving DC");
        var accurate=Apply(New("tecnica",choices:["fundamento_preciso"]),"fm-cast",option:"0",context:new(Fundamentals:["preciso","preciso"]));
        Check(accurate.Rolls[0].Total==23&&accurate.Character.Mana==46,"precise spell raises accuracy and cost");
        queue=new Queue<int>([10,1,2,7,8,8,1,6]);
        var potent=Apply(New("tecnica",choices:["fundamento_potente"]),"fm-cast",option:"0",context:new(Fundamentals:["potente"]),draw:_=>queue.Dequeue());
        Check(queue.Count==0&&potent.Message.Contains("27 de efeito")&&potent.Character.Mana==46,"potent rerolls the lowest dice up to INT and keeps better results");
        var quickTurn=Apply(New("tecnica",6,["fundamento_rapido"]),"fm-turn-start").Character;
        var quick=Apply(quickTurn,"fm-cast",option:"0",context:new(Fundamentals:["rapido"])).Character;
        Check(Get(quick,"acao_bonus")==0&&Get(quick,"acao_comum")==1&&quick.Mana==34,"quick spell downgrades common to bonus and pays surcharge");
        Reject(()=>Apply(State(quick,"acao_bonus",1),"fm-cast",option:"0",context:new(Fundamentals:["rapido"])),"quick once per round despite an extra action");
        Check(Get(Apply(Next(quick),"fm-cast",option:"0",context:new(Fundamentals:["rapido"])).Character,"fund_rapido")==1,"quick resets on next turn");
        var completeQuick=Apply(Spell(New("tecnica",6,["fundamento_rapido"]),action:"completa"),"fm-turn-start").Character;
        completeQuick=Apply(completeQuick,"fm-cast",option:"0",context:new(Fundamentals:["rapido"])).Character;
        Check(Get(completeQuick,"acao_comum")==0&&Get(completeQuick,"acao_bonus")==1,"quick full spell downgrades to common");
        var zero=Apply(Spell(New("tecnica",6,["fundamento_rapido"]),level:0,dice:"1d10"),"fm-cast",option:"0",context:new(Fundamentals:["rapido"]));
        Check(zero.Character.Mana==36,"level zero counts as level one for quick surcharge");
        var duplicate=Apply(New("tecnica",choices:["fundamento_duplicado"]),"fm-turn-start").Character;
        duplicate=Apply(duplicate,"fm-cast",option:"0",context:new(Fundamentals:["duplicado"],SpellSingleTarget:true)).Character;
        Reject(()=>Apply(State(duplicate,"acao_comum",1),"fm-cast",option:"0",context:new(Fundamentals:["duplicado"],SpellSingleTarget:true)),"duplicate once per round");
        Reject(()=>Apply(Spell(New("tecnica",choices:["fundamento_duplicado"]),"area"),"fm-cast",option:"0",context:new(Fundamentals:["duplicado"],SpellSingleTarget:true)),"area cannot be duplicated");
        var expanded=Apply(Spell(New("tecnica",choices:["fundamento_expansivo"]),"area"),"fm-cast",option:"0",context:new(Fundamentals:["expansivo"]));
        Check(expanded.Character.Mana==45&&expanded.Message.Contains("×1,5"),"expansive pays three and reports correct area multiplier");
        Check(Apply(New("tecnica",choices:["fundamento_distante"]),"fm-cast",option:"0",context:new(Fundamentals:["distante"],SpellMelee:true)).Message.Contains("9 m"),"distant converts melee range");
        Reject(()=>Apply(New("tecnica"),"fm-cast",option:"0",context:new(Fundamentals:["potente"])),"unlearned fundamental");
        Reject(()=>New("tecnica",5,["fundamento_rapido"]),"quick learning prerequisite");
        Reject(()=>New("tecnica",11,["fundamento_cruel","fundamento_preciso","fundamento_potente"]),"third fundamental requires real level twelve");
        Reject(()=>Apply(New("tecnica",6,["fundamento_rapido"]),"fm-cast",option:"0",context:new(Ritual:"simple",Improvements:["dano"],Fundamentals:["rapido"])),"undefined quick and ritual action ordering is not invented");
        Reject(()=>Apply(Spell(New("tecnica",15,["fundamento_duplicado","tecnica_maxima"]),level:6),"fm-cast",option:"0",context:new(Fundamentals:["duplicado"],SpellSingleTarget:true)),"maximum is not assumed to be numeric level six for surcharge");

        var ritual=Apply(Spell(New("tecnica",choices:["fundamento_cruel","fundamento_potente"]),"resistencia"),"fm-turn-start").Character;
        ritual=Apply(ritual,"fm-cast",option:"0",context:new(Ritual:"extended",Improvements:["cd"],Fundamentals:["cruel","cruel","potente"],SpellPowerAttribute:"sabedoria")).Character;
        Check(ritual.Mana==50&&Get(ritual,"ritual_fund_cruel")==2,"extended ritual stores fundamentals without paying early");
        ritual=Next(ritual);var released=Apply(ritual,"fm-ritual-finish");
        Check(released.Character.Mana==44&&released.Message.Contains("CD 25")&&released.Rolls.Count(r=>r.Expression=="1d8")==2,"ritual release preserves fundamental cost, DC and selected potency attribute");
        Check(!new RuleState(released.Character).State.Keys.Any(k=>k.StartsWith("ritual_fund_",StringComparison.Ordinal)),"release clears stored context");
        var distantRitual=Apply(New("tecnica",choices:["fundamento_distante"]),"fm-turn-start").Character;
        distantRitual=Apply(distantRitual,"fm-cast",option:"0",context:new(Ritual:"extended",Improvements:["dano"],Fundamentals:["distante"],SpellMelee:true)).Character;
        Check(Apply(Next(distantRitual),"fm-ritual-finish").Message.Contains("9 m"),"ritual keeps declared melee targeting");
        _=New(level:6,choices:["acervo_amplo","estilo_defensivo","estilo_duplo","estilo_distante"]);
        _=New("tecnica",8,["expansao_fundamentos","fundamento_cruel","fundamento_preciso","fundamento_potente"]);
        _=New("tecnica",12,["expansao_fundamentos","fundamento_cruel","fundamento_preciso","fundamento_potente","fundamento_distante","fundamento_rapido"]);
        Reject(()=>New(level:5,choices:["acervo_amplo"]),"broad collection prerequisite");
        Reject(()=>New("tecnica",7,["expansao_fundamentos"]),"expanded fundamentals prerequisite");
        var adept=Field(New("lutador"),"pericias",new[]{new{chave="intuicao",treino=2,outros=0},new{chave="feiticaria",treino=2,outros=0}});
        adept=Field(adept,"aptidoes",new[]{"adepto_combate","adepto_estilo_defensivo"});
        adept=Apply(adept,"fm-recalculate").Character;
        Check(FmConditions.Defense(new(adept))==20,"adept grants one style outside combat class");
        Reject(()=>Apply(Field(New("lutador"),"aptidoes",new[]{"adepto_combate","adepto_estilo_defensivo"}),"fm-recalculate"),"combat adept requires Intuition mastery");
        var mixed=Field(Field(Field(Field(New(),"nivel",16),"multiclasse",new[]{new{classe="combate",nivel=8},new{classe="tecnica",nivel=8}}),"pericias",new[]{new{chave="intuicao",treino=2,outros=0}}),"aptidoes",new[]{"adepto_combate","estilo_arremessador","adepto_estilo_duelista"});
        mixed=Apply(mixed,"fm-recalculate").Character;
        Check(FmClassChoices.StyleSteps(new(mixed),"duelista")==4&&FmClassChoices.StyleSteps(new(mixed),"arremessador")==3,"only the adept-granted style scales with full character level");
        var adeptSpell=New("lutador",choices:["adepto_feiticaria","adepto_fundamento_potente"]);
        var discountContext=new FmActionContext(Fundamentals:["potente"],UseAdeptDiscount:true);
        var discounted=Apply(adeptSpell,"fm-cast",option:"0",context:discountContext).Character;
        Check(discounted.Mana==29&&Get(discounted,"adepto_feiticaria_usos")==1,"adept grants a fundamental outside technique class and discounts only its cost");
        discounted=Apply(Apply(discounted,"fm-cast",option:"0",context:discountContext).Character,"fm-cast",option:"0",context:discountContext).Character;
        Reject(()=>Apply(discounted,"fm-cast",option:"0",context:discountContext),"adept discount limited to training uses per scene");
        discounted=Apply(discounted,"fm-combat-end").Character;
        Check(Get(Apply(discounted,"fm-cast",option:"0",context:discountContext).Character,"adepto_feiticaria_usos")==1,"new scene resets adept discount");
        Reject(()=>Apply(Field(adeptSpell,"aptidoes",new[]{"adepto_feiticaria","adepto_fundamento_rapido"}),"fm-recalculate"),"adept cannot grant quick spell");
        var adeptRitual=Apply(adeptSpell,"fm-turn-start").Character;
        adeptRitual=Apply(adeptRitual,"fm-cast",option:"0",context:discountContext with{Ritual="extended",Improvements=["dano"]}).Character;
        Check(Get(adeptRitual,"adepto_feiticaria_usos")==0,"preparing a ritual does not spend adept use early");
        var adeptRelease=Apply(Next(adeptRitual),"fm-ritual-finish").Character;
        Check(adeptRelease.Mana==29&&Get(adeptRelease,"adepto_feiticaria_usos")==1,"ritual preserves and pays adept discount once at release");
        var destruction=Field(New("tecnica",10),"foco_tecnica","destruicao");
        Check(Apply(destruction,"fm-cast",option:"0",roll:20).Message.Contains("78 de efeito"),"destruction focus counts eight critical damage dice without doubling flat bonuses");
        Console.WriteLine("F&M class options: eight styles, special strike actions/costs/criticals, blood renewal receipts, self sufficient, typed damage overrides, seven fundamentals and preserved ritual context passed.");
    }
}
