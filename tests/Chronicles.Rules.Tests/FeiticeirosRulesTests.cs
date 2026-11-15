using System.Text.Json;

public static class FeiticeirosRulesTests
{
    public static void ValidateTemplate(string path)
    {
        var system=JsonSerializer.Deserialize<SystemDefinition>(File.ReadAllText(path),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        void Check(bool valid,string label) { if(!valid)throw new Exception("F&M full template: "+label); }
        Check(SystemDefinition.ValidateLayout(system.Layout,system.Fields) is null,"layout schema");
        Check(SheetFormula.Validate(system.Fields) is null,"formulas");
        foreach(var f in system.Fields.Where(f=>f.DefaultValue is not null))Check(SheetCollectionValues.ValidField(f,f.DefaultValue!.Value),"default "+f.Key);
        var values=new Dictionary<string,JsonElement>();
        var errors=CharacterSheetValidation.ValidateFields(system,values,allowGm:true,newCharacter:true);
        Check(errors is null,"new character with empty input gets valid defaults: "+JsonSerializer.Serialize(errors));
        var state=new CharacterSheetState("Ficha vazia",null,null,10,10,10,10,10,10,values);
        var plugin=RpgPluginRegistry.Default.RulesFor(system)!;
        state=plugin.Execute(system,state,new("fm-initialize"),sides=>sides).Character;
        errors=CharacterSheetValidation.ValidateFields(system,state.Fields,values,true,ruleExecution:true,ruleValidator:plugin);
        Check(errors is null,"executed state round-trips through server field validation: "+JsonSerializer.Serialize(errors));
        Check(state.Health==12 && state.Mana==4,"default lutador resources");
        Check(plugin.Describe(system,state).Actions.Length>=25,"rule view available with full template");
        Console.WriteLine("F&M full frontend template: backend defaults, layout, field constraints, formulas, initialization and rule-result validation passed.");
    }
    public static void Run()
    {
        var plugin=new FmRulesPlugin();
        var field=new SystemField("estado_regras","Estado","table","Regras",null,null,null,Columns:[new("chave","Chave","text"),new("valor","Valor","number")]);
        var system=new SystemDefinition([field],null,FmCatalog.Id,1,FmCatalog.Edition);
        CharacterInput New(string specialization="lutador",string origin="inato",int level=1,int con=14) => new("Feiticeiro de teste",null,null,10,10,0,0,10,10,new Dictionary<string,object>{["origem"]=origin,["especializacao"]=specialization,["atributo_tecnica"]="sabedoria",["atributo_especializacao"]=specialization is "tecnica"?"inteligencia":specialization is "suporte" or "controlador"?"sabedoria":"forca",["nivel"]=level,["forca"]=16,["destreza"]=12,["constituicao"]=con,["inteligencia"]=16,["sabedoria"]=16,["presenca"]=12,["natureza"]="humano",["defesa"]=16,["deslocamento"]=9}.ToDictionary(kv=>kv.Key,kv=>JsonSerializer.SerializeToElement(kv.Value)));
        CharacterInput Values(CharacterInput c,params(string Key,object Value)[] pairs) { var values=c.Fields!.ToDictionary(kv=>kv.Key,kv=>kv.Value.Clone()); foreach(var (key,value) in pairs) values[key]=JsonSerializer.SerializeToElement(value); return c with { Fields=values }; }
        CharacterInput State(CharacterInput c,params(string Key,int Value)[] pairs) { var s=new RuleState(c); foreach(var (key,value) in pairs)s.Set(key,value); return s.Export(); }
        RuleResult Apply(CharacterInput c,string action,int amount=0,string? option=null,int roll=10) => plugin.Execute(system,c,new(action,amount,option),sides=>Math.Min(sides,roll));
        CharacterInput Init(CharacterInput c) => Apply(c,"fm-initialize").Character;
        int Counter(CharacterInput c,string key)=>new RuleState(c).Get(key);
        void Check(bool valid,string label) { if(!valid)throw new Exception("F&M: "+label); }
        void Reject(Action action,string label) { try { action(); }catch(ArgumentException){return;}throw new Exception("F&M accepted invalid action: "+label); }

        Check(RpgPluginRegistry.Default.RulesFor(system) is FmRulesPlugin,"registered independent engine");
        Check(RpgPluginRegistry.Default.Resolve(system with {RulesetVersion="2.6"}) is null,"unknown edition rejected");
        Check(RpgPluginRegistry.Default.RulesFor(system with {TemplateVersion=2}) is null,"unknown schema rejected");
        foreach(var definition in FmCatalog.Classes)
        {
            var c=Init(New(definition.Id,definition.Id=="restringido"?"restringido":"inato",5));
            Check(c.MaxHealth==definition.InitialHealth+4*definition.HealthGain+10,definition.Name+" HP progression");
            Check(c.MaxMana==5*definition.EnergyGain+(definition.Id is "tecnica" or "controlador" or "suporte"?3:0),definition.Name+" energy progression");
            Reject(()=>Apply(c,"fm-initialize"),"initialization cannot refill resources");
        }
        Reject(()=>Init(New("restringido")),"restricted origin required");
        Reject(()=>Init(New("tecnica","sem_tecnica")),"no-technique class restriction");
        var restricted=Init(New("restringido","restringido",4));
        Check(restricted.MaxMana==16,"stamina instead of PE");
        var multi=Values(New("lutador",level:3),("multiclasse",new[]{new{classe="lutador",nivel=2},new{classe="tecnica",nivel=1}}));
        Check(Init(multi).MaxHealth==29 && Init(multi).MaxMana==14,"multiclass first-level HP and no additional starting modifier");
        Reject(()=>Init(Values(multi,("inteligencia",10),("sabedoria",10))),"multiclass prerequisite");
        Reject(()=>Init(Values(multi,("nivel",5))),"level sum mismatch");

        var c1=Init(New("combate",level:4)); var spent=Apply(c1,"fm-spend",3).Character;
        var leveled=Apply(Values(spent,("nivel",5),("constituicao",16)),"fm-recalculate").Character;
        Check(leveled.MaxHealth==51 && leveled.Health==51 && leveled.MaxMana==20 && leveled.Mana==17,"retroactive CON and resource deficits preserved");
        Reject(()=>Apply(spent,"fm-spend",9999),"insufficient resources");
        var temporary=Apply(c1,"fm-temporary",999,"vida").Character;
        temporary=Apply(temporary,"fm-temporary",1,"vida").Character;
        Check(Counter(temporary,"vida_temp")==c1.MaxHealth/2,"temporary resources capped and do not stack");
        var hp=temporary.Health; var damaged=Apply(temporary,"fm-damage",3,"perda_vida").Character;
        Check(damaged.Health==hp-3 && Counter(damaged,"vida_temp")==Counter(temporary,"vida_temp"),"life loss bypasses temporary HP");
        var defenses=Values(c1,("defesas",new[]{new{tipo="queimante",efeito="resistencia",valor=0,fonte="a"},new{tipo="queimante",efeito="resistencia",valor=0,fonte="b"},new{tipo="queimante",efeito="rd",valor=4,fonte="escudo"}}));
        var fire=Apply(defenses,"fm-damage",21,"queimante").Character;
        Check(fire.Health==c1.Health-6,"resistance does not stack and precedes flat reduction");
        Check(Apply(Init(New()),"fm-damage",20,"reversa").Character.Health==14,"human immune to reverse energy damage");
        var curse=Values(c1,("natureza","maldicao"));
        Check(Apply(curse,"fm-damage",10,"reversa").Character.Health==c1.Health-15,"curse vulnerable at 1.5x");
        Reject(()=>Apply(curse,"fm-heal",4,"reversa"),"curse cannot be healed by reverse energy");
        var fragile=State(defenses,("cond_fragilizado",-1));
        Check(Apply(fragile,"fm-damage",10,"queimante").Character.Health==c1.Health-10,"fragile suppresses RD and resistance");
        var immune=Values(fragile,("defesas",new[]{new{tipo="queimante",efeito="imunidade",valor=0,fonte="imune"}}));
        Check(Apply(immune,"fm-damage",999,"queimante").Character.Health==immune.Health,"fragile preserves immunity");

        var soul=State(Init(New(level:10)),("cd_integridade",999)); var oldMax=soul.MaxHealth;
        soul=Apply(soul,"fm-temporary",10,"vida").Character; soul=Apply(soul,"fm-damage",10,"alma",roll:1).Character;
        Check(soul.MaxHealth==oldMax-10 && soul.Health==oldMax-10 && Counter(soul,"vida_temp")==10 && Counter(soul,"alma_dano")==10,"soul bypass, simultaneous max/current/integrity loss");
        soul=Apply(soul,"fm-long-rest",option:"normal").Character;
        Check(soul.MaxHealth==oldMax-5,"long rest recovers half maximum HP lost to soul");
        var save=Values(c1,("resistencias",new[]{new{chave="integridade",treino=2,outros=0}}));
        save=State(save,("cd_integridade",12));
        Check(Apply(save,"fm-damage",10,"alma",roll:20).Character.MaxHealth==save.MaxHealth,"master critical integrity save negates soul damage");
        var exact=State(c1,("alma_max",40),("alma_dano",10)); Check(FmConditions.SoulStage(new(exact))==0,"75 percent boundary follows below wording");
        Check(FmConditions.SoulStage(new(State(c1,("alma_max",40),("alma_dano",11))))==1,"below 75 percent damaged");

        var dying=Apply(Init(New()),"fm-damage",20,"perda_vida").Character;
        Check(dying.Health==0 && Counter(dying,"vida_negativa")==6,"negative HP deficit persists");
        dying=Apply(dying,"fm-death-save",roll:1).Character; Check(Counter(dying,"morte_falhas")==2,"natural one gives two failures");
        dying=Apply(dying,"fm-heal",3,"comum").Character; Check(Counter(dying,"morrendo")==1 && Counter(dying,"vida_negativa")==3,"healing must repay deficit");
        dying=Apply(dying,"fm-heal",3,"comum").Character; Check(dying.Health==1 && Counter(dying,"morrendo")==0 && Counter(dying,"morte_falhas")==2,"stabilization preserves death failures");
        var instant=Apply(Init(New()),"fm-damage",29,"perda_vida").Character; Check(Counter(instant,"morto")==1,"massive negative damage causes death");
        var deadly=Apply(Apply(Init(New()),"fm-damage",14,"perda_vida").Character,"fm-death-save",roll:1).Character;
        deadly=Apply(deadly,"fm-damage",1,"perda_vida").Character; Check(Counter(deadly,"morto")==1,"damage while dying adds failure");

        var poisonFear=State(c1,("cond_envenenado",-1),("cond_amedrontado",-1));
        var skill=FmCatalog.Skills.Single(v=>v.Id=="atletismo");
        Check(FmConditions.Bonus(new(poisonFear),skill)==FmConditions.Bonus(new(c1),skill)-3,"same condition penalties use worst");
        var restrained=State(c1,("cond_agarrado",-1)); var active=FmConditions.Active(new(restrained));
        Check(active.Contains("imovel") && active.Contains("desprevenido") && FmConditions.Defense(new(restrained))==13,"derived grapple conditions");
        Reject(()=>Apply(c1,"fm-check",10,"per_feticaria"),"unknown check rejected");
        Reject(()=>Apply(c1,"fm-check",10,"per_fеитicaria"),"noncanonical check rejected");
        Reject(()=>Apply(c1,"fm-check",10,"per_feiticaria"),"trained-only skill enforced");
        var turn=Apply(c1,"fm-turn-start").Character; turn=Apply(turn,"fm-action",option:"completa").Character;
        Check(Counter(turn,"acao_comum")==0 && Counter(turn,"acao_bonus")==0 && Counter(turn,"acao_movimento")==1,"full action consumes common and bonus");
        Reject(()=>Apply(turn,"fm-turn-start"),"cannot reset actions by repeating start");
        Reject(()=>Apply(turn,"fm-action",option:"comum"),"spent action cannot be reused");
        turn=Apply(Apply(turn,"fm-turn-end").Character,"fm-turn-start").Character;
        turn=Apply(turn,"fm-action",option:"bonus_movimento").Character;
        Check(Counter(turn,"acao_movimento")==2 && Counter(turn,"acao_bonus")==0,"action hierarchy conversion");
        Reject(()=>Apply(State(turn,("cond_enjoado",-1)),"fm-action",option:"comum_movimento"),"nausea blocks action conversion");
        var sequence=Apply(c1,"fm-sequence",20).Character; Check(FmConditions.Defense(new(sequence))==11,"sequence defense penalty capped at five");
        sequence=Apply(sequence,"fm-turn-start").Character; Check(Counter(sequence,"sequencia")==0,"sequence clears at target turn");

        var weapon=Values(c1,("armas",new[]{new{nome="Lâmina",atributo="forca",dados="1d6+2",apos="1d4",treinado=true,margem=19,acao="comum",municao=2}}));
        var critical=Apply(weapon,"fm-attack",20,"0",20);
        Check(critical.Message.Contains("21 de dano") && FmCatalog.Number(FmCatalog.Rows(new(critical.Character),"armas")[0],"municao")==1,"critical dice only, damage-after once, ammunition decremented");
        var miss=Apply(weapon,"fm-attack",999,"0",19); Check(!miss.Message.Contains("crítico"),"expanded margin does not guarantee a hit");

        var spell=Values(c1,("feiticos",new[]{new{nome="Projeção",nivel=2,tipo="ataque",acao="comum",duracao="imediata",dados="8d8",reducao=99}}));
        Reject(()=>Apply(spell,"fm-cast",0,"0"),"spell level access");
        spell=Values(spell,("nivel",5)); spell=Init(Values(New(level:5),("feiticos",FmCatalog.Rows(new(spell),"feiticos"))));
        Check(Apply(spell,"fm-cast",0,"0").Character.Mana==spell.Mana-1,"spell minimum cost is one");
        Reject(()=>Apply(State(spell,("exaustao_tecnica",1)),"fm-cast",0,"0"),"burnout prevents casting");
        var domain=Values(Init(New("tecnica",level:14)),("apt_dominio",4),("apt_barreira",4),("aptidoes",new[]{"expansao_incompleta","expansao_completa","tecnicas_barreira","acerto_garantido"}),("pericias",new[]{new{chave="feiticaria",treino=1,outros=0}}));
        domain=Apply(domain,"fm-domain",option:"garantido").Character; Check(domain.Mana==62 && Counter(domain,"dominio_rodadas")==7,"guaranteed domain cost and duration");
        domain=Apply(domain,"fm-domain-end").Character; Check(Counter(domain,"exaustao_tecnica")==4,"domain burnout");
        Reject(()=>Apply(domain,"fm-domain",option:"garantido"),"domain cannot reopen during burnout");

        var invalidState=Values(c1,("estado_regras",new[]{new{chave="exaustao",valor=99}}));
        Check(plugin.ValidateStateChange(system,c1.Fields!,invalidState.Fields!,true,false,false) is not null,"direct invalid state rejected");
        Check(plugin.ValidateStateChange(system,c1.Fields!,State(c1,("morte_falhas",1)).Fields!,true,false,false) is not null,"PUT cannot silently rewrite combat ledger");
        Check(plugin.ValidateStateChange(system,[],c1.Fields!,true,false,true) is not null,"new import cannot forge executed state");
        var malformed=Values(c1,("pericias",new[]{1,2})); Check(plugin.ValidateStateChange(system,[],malformed.Fields!,true,false,true) is not null,"malformed rows produce validation error");
        Console.WriteLine("F&M 2.5.2: creation/resources, classes, multiclass, soul, conditions, death, actions, spells, domains and state-validation tests passed.");
    }
}
