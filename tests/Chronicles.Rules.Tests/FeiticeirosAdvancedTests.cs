using System.Text.Json;

public static class FeiticeirosAdvancedTests
{
    public static void Run()
    {
        var engine=new FmRulesPlugin();
        var system=new SystemDefinition([new("estado_regras","Estado","table","Regras",null,null,null)],null,FmCatalog.Id,1,"2.5.2");
        CharacterInput New(int level=10,string[]? abilities=null)
        {
            var values=new Dictionary<string,object>{["origem"]="inato",["especializacao"]="combate",["atributo_tecnica"]="sabedoria",["atributo_especializacao"]="forca",["nivel"]=level,["forca"]=16,["destreza"]=14,["constituicao"]=14,["sabedoria"]=14,["inteligencia"]=16,["presenca"]=14,["natureza"]="humano",["defesa"]=16,["apt_aura"]=2,["apt_controle"]=3,["apt_barreira"]=2,["apt_dominio"]=2,["apt_reversa"]=2,["aptidoes"]=abilities??[],["pericias"]=new[]{new{chave="prestidigitacao",treino=1,outros=0}},["armas"]=new[]{new{nome="Teste",atributo="forca",dados="1d6+2",apos="1d4",treinado=true,margem=20,acao="comum",municao=-1}},["feiticos"]=new[]{new{nome="Teste",nivel=1,tipo="ataque",acao="comum",duracao="imediata",dados="4d8",reducao=0}}};
            var state=new CharacterInput("Ensaio avançado",null,null,10,10,0,0,10,10,values.ToDictionary(kv=>kv.Key,kv=>JsonSerializer.SerializeToElement(kv.Value)));
            return engine.Execute(system,state,new("fm-initialize"),sides=>sides).Character;
        }
        RuleResult Apply(CharacterInput state,string action,int amount=0,string? option=null,int roll=20,FmActionContext? context=null)=>engine.Execute(system,state,new(action,amount,option,Jujutsu:context),sides=>Math.Min(sides,roll));
        CharacterInput Field(CharacterInput c,string key,object value)=>c with{Fields=new(c.Fields!){[key]=JsonSerializer.SerializeToElement(value)}};
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        int Get(CharacterInput c,string key)=>new RuleState(c).Get(key);
        void Check(bool success,string label){if(!success)throw new Exception("F&M advanced: "+label);}
        void Reject(Action action,string label){try{action();}catch(ArgumentException){return;}throw new Exception("F&M advanced accepted invalid: "+label);}

        var empty=New() with{Fields=New().Fields!.Where(kv=>kv.Key!="estado_regras").ToDictionary(kv=>kv.Key,kv=>kv.Value)};
        var creation=Apply(empty,"fm-attribute-start",option:"pontos").Character;
        creation=Field(creation,"criacao_atributos",FmCatalog.Attributes.Select((chave,i)=>new{chave,base_valor=new[]{15,14,13,10,8,10}[i],bonus=i==0?2:i==1?1:0}).ToArray());
        var completed=Apply(creation,"fm-attribute-finish").Character;
        Check(new RuleState(completed).N("forca")==17&&new RuleState(completed).N("destreza")==15,"guided point buy applies base plus origin bonuses");
        Reject(()=>Apply(completed,"fm-attribute-finish"),"guided attributes cannot be repeatedly applied");
        Reject(()=>Apply(Field(creation,"criacao_atributos",FmCatalog.Attributes.Select((chave,i)=>new{chave,base_valor=15,bonus=i==0?2:i==1?1:0}).ToArray()),"fm-attribute-finish"),"point buy enforces 17 not D&D's 27 points");
        var rolled=Apply(empty,"fm-attribute-start",option:"rolagem",roll:6);
        Check(rolled.Rolls.Length==6&&rolled.Rolls.All(r=>r.Rolls.Length==4&&r.KeptIndexes.Length==3&&r.Total==18),"six 4d6 rolls drop exactly one minimum");
        var fixedStart=Apply(empty,"fm-attribute-start",option:"fixos").Character;
        var fixedRows=FmCatalog.Rows(new(fixedStart),"criacao_atributos");
        fixedStart=Field(fixedStart,"criacao_atributos",fixedRows.Select((row,i)=>new{chave=FmCatalog.Text(row,"chave"),base_valor=FmCatalog.Number(row,"base_valor"),bonus=i==0?2:i==1?1:0}).ToArray());
        Check(Get(Apply(fixedStart,"fm-attribute-finish").Character,"criacao_concluida")==1,"fixed-array allocation and origin validation");
        Reject(()=>Apply(Field(fixedStart,"criacao_atributos",FmCatalog.Attributes.Select((chave,i)=>new{chave,base_valor=15,bonus=i==0?2:i==1?1:0}).ToArray()),"fm-attribute-finish"),"fixed values cannot be duplicated");
        var inherited=Field(Field(empty,"origem","herdado"),"heranca_cla","gojo");inherited=Apply(inherited,"fm-attribute-start",option:"fixos").Character;
        inherited=Field(inherited,"criacao_atributos",FmCatalog.Attributes.Select((chave,i)=>new{chave,base_valor=new[]{15,14,13,12,10,8}[i],bonus=chave=="forca"?2:chave=="destreza"?1:0}).ToArray());
        Reject(()=>Apply(inherited,"fm-attribute-finish"),"Gojo inheritance cannot assign its bonuses to STR/DEX");

        var channel=New(abilities:["canalizar_golpe"]);channel=Apply(channel,"fm-channel",3).Character;
        Check(Get(channel,"canalizacao_pe")==3&&channel.Mana==37,"channel pays and persists for next hit");
        Check(Get(Apply(channel,"fm-combat-end").Character,"canalizacao_pe")==3,"ending a scene does not invent an expiry for the next-hit channel");
        var missed=Apply(channel,"fm-attack",999,"0",roll:2,context:new(AttackKind:"melee")).Character;
        Check(Get(missed,"canalizacao_pe")==3,"miss preserves channel");
        var hit=Apply(missed,"fm-attack",0,"0",context:new(AttackKind:"melee"));
        Check(Get(hit.Character,"canalizacao_pe")==0&&hit.Rolls.Any(r=>r.Expression=="6d6"),"channel is damage-during and doubles on critical");
        Reject(()=>Apply(New(abilities:["canalizar_golpe"]),"fm-channel",4),"channel CL limit");
        var covered=Apply(New(abilities:["cobrir_se"]),"fm-cover",8).Character;
        Check(Get(covered,"cobertura_temp")==32,"cover independent temporary HP");
        covered=Apply(covered,"fm-temporary",10,"vida").Character;
        covered=Apply(covered,"fm-damage",20,"impacto").Character;
        Check(Get(covered,"cobertura_temp")==12&&Get(covered,"vida_temp")==10,"cover is consumed before persistent temporary HP");
        covered=Apply(covered,"fm-cover-end").Character;Check(Get(covered,"cobertura_temp")==0,"source turn expiry explicit");
        Check(Apply(New(abilities:["tecnicas_barreira"]),"fm-barrier",6).Message.Contains("15 PV por parede"),"barrier health formula");
        Reject(()=>Apply(New(abilities:["tecnicas_barreira"]),"fm-barrier",7),"barrier wall limit");
        var simple=Apply(New(abilities:["dominio_simples"]),"fm-simple-domain").Character;
        Check(Get(simple,"simples_durabilidade")==3&&Get(simple,"simples_raio_meios")==3,"simple domain durability and range");
        simple=Apply(simple,"fm-concentration",999,"testar",1).Character;
        Check(Get(simple,"concentrando")==1&&Get(simple,"simples_durabilidade")==2,"failed concentration erodes simple domain instead of immediately ending it");
        simple=Apply(Apply(simple,"fm-antidomain-loss").Character,"fm-antidomain-loss").Character;
        Check(Get(simple,"simples_durabilidade")==0&&Get(simple,"concentrando")==0,"simple domain breaks when durability/range reaches zero");
        var immune=State(Field(New(),"imunidades_condicao",new[]{"agarrado"}),"cond_agarrado",-1);
        Check(!FmConditions.Active(new(immune)).Contains("agarrado")&&FmConditions.Active(new(immune)).Contains("imovel"),"condition immunity does not grant immunity to implied conditions");

        var ritual=Apply(New(),"fm-turn-start").Character;
        var start=Apply(ritual,"fm-cast",0,"0",context:new(Ritual:"extended",Improvements:["dano","dano","precisao","cd","alcance"]));
        Check(Get(start.Character,"ritual_spell")==1&&Get(start.Character,"acao_comum")==0&&Get(start.Character,"acao_bonus")==0&&Get(start.Character,"acao_movimento")==0,"extended ritual first-turn action budget");
        Check(start.Character.Mana==ritual.Mana&&FmConditions.Active(new(start.Character)).Contains("desprevenido"),"extended ritual concentrates before paying release cost");
        Reject(()=>Apply(start.Character,"fm-ritual-finish"),"extended ritual cannot finish on same turn");
        var next=Apply(Apply(start.Character,"fm-turn-end").Character,"fm-turn-start").Character;
        var finished=Apply(next,"fm-ritual-finish");
        Check(Get(finished.Character,"ritual_spell")==0&&finished.Character.Mana==38&&Get(finished.Character,"acao_comum")==0&&Get(finished.Character,"acao_bonus")==0,"extended ritual finishes on next turn and pays once");
        var broken=Apply(start.Character,"fm-concentration",999,"testar",1).Character;
        Check(Get(broken,"ritual_spell")==0&&Get(broken,"concentrando")==0,"losing concentration cancels extended ritual");
        var wrong=Field(start.Character,"feiticos",new[]{new{nome="Outra técnica",nivel=1,tipo="ataque",acao="comum",duracao="imediata",dados="4d8",reducao=0}});
        Check(engine.ValidateStateChange(system,start.Character.Fields!,wrong.Fields!,true,false,false) is not null,"pending ritual freezes spell identity");
        var failed=Apply(ritual,"fm-cast",0,"0",roll:1,context:new(Ritual:"simple",Improvements:["dano","cd"],RitualFailure:"cancel"));
        Check(failed.Character.Mana==ritual.Mana&&Get(failed.Character,"acao_comum")==0&&Get(failed.Character,"ritual_spell")==0,"failed cancelled ritual consumes action, not PE");
        var delayed=Apply(ritual,"fm-cast",0,"0",roll:1,context:new(Ritual:"simple",Improvements:["dano"],RitualFailure:"delay")).Character;
        Check(Get(delayed,"ritual_mode")==1,"failed ritual can be delayed");
        delayed=Apply(Apply(delayed,"fm-turn-end").Character,"fm-turn-start").Character;
        Check(Get(delayed,"ritual_spell")==0&&delayed.Mana==38&&Get(delayed,"acao_comum")==1,"delayed simple ritual releases at turn start without consuming a second common action");
        Reject(()=>Apply(ritual,"fm-cast",0,"0",context:new(Ritual:"simple",Improvements:["dano","dano","dano"])),"ritual improvement count limit");
        Reject(()=>Apply(ritual,"fm-cast",0,"0",context:new(Ritual:"simple",Improvements:["sustento"])),"sustain conversion requires sustained spell");

        var kokusen=New(abilities:["raio_negro"]);var beforeMana=kokusen.Mana;
        var black=Apply(kokusen,"fm-attack",0,"0",context:new(AttackKind:"melee"));
        Check(black.Message.Contains("Raio Negro")&&black.Message.Contains("29 de dano"),"black flash multiplies damage-during by 1.5 then adds damage-after");
        Check(Apply(State(kokusen,"exaustao",2),"fm-attack",0,"0",context:new(AttackKind:"melee")).Message.Contains("26 de dano"),"roll penalties precede black flash amplification");
        Check(black.Character.MaxMana==50&&black.Character.Mana==beforeMana&&new RuleState(black.Character).N("apt_aura")==3,"first black flash awakens maximum PE and aura without refilling spent PE");
        var range=Apply(kokusen,"fm-attack",0,"0",context:new(AttackKind:"ranged"));
        Check(!range.Message.Contains("Raio Negro")&&Get(range.Character,"kokusen_desperto")==0,"ranged attack cannot trigger black flash");
        var lower=Apply(black.Character,"fm-attack",0,"0",roll:19,context:new(AttackKind:"melee"));
        Check(lower.Message.Contains("Raio Negro")&&lower.Character.MaxMana==50,"consecutive black flash lowers threshold but does not repeat permanent reward");
        var guard=Apply(black.Character,"fm-attack",999,"0",roll:19,context:new(AttackKind:"melee"));
        Check(!guard.Message.Contains("Raio Negro"),"lowered black flash threshold does not bypass target defense");
        Reject(()=>Apply(kokusen,"fm-attack",0,"0",context:new(AttackKind:"offhand")),"offhand light weapon confirmation");
        var conceal=Apply(kokusen,"fm-attack",0,"0",roll:20,context:new(AttackKind:"melee",Concealment:50));
        Check(!conceal.Message.Contains("Raio Negro"),"concealment can prevent even a critical hit before black flash");

        var maximum=Field(Field(New(17,["tecnica_maxima"]),"pericias",new[]{new{chave="feiticaria",treino=2,outros=0}}),"feiticos",new[]{new{nome="Máxima",nivel=6,tipo="area",acao="comum",duracao="imediata",dados="22d10",reducao=0}});
        maximum=Apply(maximum,"fm-turn-start").Character;maximum=Apply(maximum,"fm-cast",0,"0").Character;
        Check(Get(maximum,"maxima_recarga")==3,"maximum technique cooldown uses half BT");
        maximum=Apply(maximum,"fm-turn-end").Character;Check(Get(maximum,"maxima_recarga")==3,"casting turn does not consume a full waiting round");
        Reject(()=>Apply(maximum,"fm-cast",0,"0"),"maximum cooldown cannot be bypassed");
        Console.WriteLine("F&M advanced: rituals, delayed/extended casting, concentration, maximum cooldown, channel/cover, barriers, anti-domain durability, condition immunity, black flash and contextual attacks passed.");
    }
}
