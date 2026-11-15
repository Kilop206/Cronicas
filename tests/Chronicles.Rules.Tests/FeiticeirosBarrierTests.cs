using System.Text.Json;

public static class FeiticeirosBarrierTests
{
    public static void Run()
    {
        var engine=new FmRulesPlugin();
        var system=new SystemDefinition([new("estado_regras","Estado","table","Regras",null,null,null)],null,FmCatalog.Id,1,"2.5.2");
        RuleResult Apply(CharacterInput c,string action,int amount=0,string? option=null,FmActionContext? context=null,int roll=10)=>engine.Execute(system,c,new(action,amount,option,Jujutsu:context),sides=>Math.Min(sides,roll));
        CharacterInput Field(CharacterInput c,string key,object value)=>c with{Fields=new(c.Fields!){[key]=JsonSerializer.SerializeToElement(value)}};
        CharacterInput State(CharacterInput c,string key,int value){var s=new RuleState(c);s.Set(key,value);return s.Export();}
        int Get(CharacterInput c,string key)=>new RuleState(c).Get(key);
        void Check(bool test,string label){if(!test)throw new Exception("F&M barriers: "+label);}
        void Reject(Action action,string label){try{action();}catch(ArgumentException){return;}throw new Exception("F&M barriers accepted invalid: "+label);}
        CharacterInput New(string[]? abilities=null,int level=8)
        {
            var values=new Dictionary<string,object>{["origem"]="inato",["especializacao"]="combate",["atributo_tecnica"]="sabedoria",["atributo_especializacao"]="forca",["nivel"]=level,["forca"]=16,["destreza"]=14,["constituicao"]=14,["sabedoria"]=14,["inteligencia"]=14,["natureza"]="humano",["apt_barreira"]=3,["apt_dominio"]=2,["aptidoes"]=abilities??["tecnicas_barreira"],
                ["pericias"]=new[]{new{chave="historia",treino=2,outros=0},new{chave="prestidigitacao",treino=1,outros=0}},
                ["armas"]=new[]{new{nome="Ensaio",atributo="forca",dados="1d6",apos="",treinado=true,margem=20,acao="comum",municao=-1}}};
            return Apply(new("Barreiras",null,null,1,1,0,0,0,0,values.ToDictionary(kv=>kv.Key,kv=>JsonSerializer.SerializeToElement(kv.Value))),"fm-initialize").Character;
        }
        CharacterInput Next(CharacterInput c,FmActionContext? context=null)=>Apply(Apply(c,"fm-turn-end").Character,"fm-turn-start",context:context).Character;
        var original=New();var walls=Apply(original,"fm-barrier",3).Character;
        Check(walls.Mana==original.Mana-3&&Get(walls,"parede_1")==17&&Get(walls,"parede_3")==17,"individual wall HP and cost");
        walls=Apply(walls,"fm-barrier",2).Character;
        Check(Get(walls,"parede_1")==17&&Get(walls,"parede_5")==17,"additional casting preserves previous walls");
        walls=Apply(walls,"fm-barrier-damage",10,"parede_1").Character;
        Check(Get(walls,"parede_1")==7&&Get(walls,"parede_2")==17&&walls.Health==original.Health,"damage belongs only to selected wall");
        walls=Apply(walls,"fm-barrier-damage",7,"parede_1").Character;
        Check(Get(walls,"parede_1")==0,"destroyed wall removed");Reject(()=>Apply(walls,"fm-barrier-damage",1,"parede_1"),"destroyed wall cannot be targeted again");
        var moving=Apply(walls,"fm-turn-start").Character;var mana=moving.Mana;
        moving=Apply(moving,"fm-barrier-move",option:"parede_2").Character;
        Check(Get(moving,"acao_comum")==0&&moving.Mana==mana,"moving costs common action and no PE");
        Reject(()=>Apply(moving,"fm-barrier-move",option:"parede_2"),"moving cannot reuse common action");
        var quick=Apply(New(["tecnicas_barreira","paredes_resistentes","barreira_rapida"]),"fm-turn-start").Character;
        quick=Apply(quick,"fm-barrier",1).Character;
        Check(Get(quick,"parede_1")==34&&Get(quick,"acao_bonus")==0&&Get(quick,"acao_comum")==1,"enhanced walls and quick creation");
        quick=Apply(Next(quick),"fm-barrier-move",option:"parede_1").Character;
        Check(Get(quick,"acao_bonus")==0&&Get(quick,"acao_comum")==1,"quick manipulation");
        Reject(()=>Apply(New(),"fm-barrier",7),"maximum six walls in one creation");Reject(()=>Apply(New(),"fm-barrier",0),"positive wall count");
        Reject(()=>Apply(Field(New(),"apt_barreira",0),"fm-barrier",1),"barrier aptitude prerequisite");
        Reject(()=>Apply(New(["tecnicas_barreira","paredes_resistentes"],3),"fm-barrier",1),"enhanced prerequisite");
        var curtain=Apply(New(["tecnicas_barreira","cortina"]),"fm-curtain",3,"comum").Character;
        Check(Get(curtain,"cortina_1")==51&&curtain.Mana==29,"curtain has three-wall HP and coverage cost");
        curtain=Apply(curtain,"fm-curtain",1,"livre").Character;
        Check(Get(curtain,"cortina_1")==51&&Get(curtain,"cortina_2")==51,"multiple curtains preserved");
        Reject(()=>Apply(curtain,"fm-curtain",1),"curtain action requires explicit table ruling");
        Reject(()=>Apply(curtain,"fm-barrier-move",option:"cortina_1"),"wall manipulation does not imply curtain manipulation");
        curtain=Apply(curtain,"fm-barrier-dismiss",option:"cortina_1").Character;
        Check(Get(curtain,"cortina_1")==0&&Get(curtain,"cortina_2")==51,"manual end only removes selected curtain");
        Check(Get(Apply(curtain,"fm-combat-end").Character,"cortina_2")==51,"barriers persist until destroyed or source ends");
        var enhancedCurtain=Apply(New(["tecnicas_barreira","cortina","paredes_resistentes"]),"fm-curtain",1,"comum").Character;
        Check(Get(enhancedCurtain,"cortina_1")==102,"curtain inherits enhanced wall HP");
        var turn=Apply(New(["dominio_simples","cesta_oca_vime"]),"fm-turn-start").Character;
        turn=Apply(turn,"fm-turn-end").Character;
        var simple=Apply(turn,"fm-simple-domain",option:"reacao").Character;
        Check(Get(simple,"acao_reacao")==0&&Get(simple,"simples_durabilidade")==4&&simple.Mana==27,"simple domain reacts outside own turn");
        Reject(()=>Apply(simple,"fm-basket",option:"reacao"),"anti-domain reaction cannot be reused");
        simple=Apply(simple,"fm-turn-start",context:new(GuaranteedHitExposure:true)).Character;
        Check(Get(simple,"simples_durabilidade")==3&&Get(simple,"simples_raio_meios")==2,"start-turn exposure erodes durability and radius once");
        Reject(()=>Apply(simple,"fm-turn-start",context:new(GuaranteedHitExposure:true)),"turn start cannot be replayed for exposure");
        simple=Next(Next(simple,new(GuaranteedHitExposure:true)),new(GuaranteedHitExposure:true));
        Check(Get(simple,"simples_durabilidade")==0&&Get(simple,"concentrando")==0,"simple domain breaks at zero radius before durability runs out");
        var basket=Apply(New(["cesta_oca_vime","dominio_simples"]),"fm-basket").Character;
        Check(basket.Mana==29&&Get(basket,"cesta_durabilidade")==4,"basket activation cost and durability");
        basket=Apply(basket,"fm-turn-start",context:new(GuaranteedHitExposure:true)).Character;
        Check(Get(basket,"cesta_durabilidade")==3,"unsealed basket loses durability on exposure");
        basket=Next(basket,new(GuaranteedHitExposure:true,MaintainBasketSeal:true));
        Check(Get(basket,"cesta_durabilidade")==3&&Get(basket,"cesta_selo")==1,"held seal protects against exposure");
        Reject(()=>Apply(basket,"fm-attack",option:"0"),"held seal occupies both weapon hands");
        basket=Apply(basket,"fm-concentration",999,"testar",roll:1).Character;
        Check(Get(basket,"cesta_durabilidade")==2&&Get(basket,"concentrando")==1,"held seal still loses durability on concentration failure");
        basket=Next(basket,new(GuaranteedHitExposure:true));
        Check(Get(basket,"cesta_durabilidade")==1&&Get(basket,"cesta_selo")==0,"next turn can release seal and lose durability");
        basket=Apply(basket,"fm-concentration",999,"testar",roll:1).Character;
        Check(Get(basket,"cesta_durabilidade")==0&&Get(basket,"concentrando")==0,"basket breaks at zero durability");
        var history=Field(New(["cesta_oca_vime"]),"pericias",new[]{new{chave="historia",treino=1,outros=0}});
        Reject(()=>Apply(history,"fm-basket"),"basket history mastery prerequisite");
        history=Field(history,"aptidoes",new[]{"cesta_oca_vime","cesta_epoca"});
        Check(Get(Apply(history,"fm-basket").Character,"cesta_durabilidade")==4,"narrator-confirmed historical origin alternative");
        Reject(()=>Apply(New(["cesta_oca_vime"],4),"fm-basket"),"basket minimum level");
        var sealedBasket=Apply(Apply(New(["cesta_oca_vime","dominio_simples"]),"fm-basket").Character,"fm-turn-start",context:new(MaintainBasketSeal:true)).Character;
        Check(Get(Apply(sealedBasket,"fm-simple-domain",option:"reacao").Character,"cesta_selo")==0,"new concentration removes old basket and occupied seal");
        Check(Get(Apply(sealedBasket,"fm-condition",1,"atordoado").Character,"cesta_durabilidade")==0,"incapacitation ends basket rather than eroding it");
        Check(Get(Apply(sealedBasket,"fm-concentration",option:"encerrar").Character,"cesta_selo")==0,"explicit concentration end frees seal");
        Reject(()=>Apply(State(Apply(New(["cesta_oca_vime"]),"fm-basket").Character,"maos_livres",1),"fm-turn-start",context:new(MaintainBasketSeal:true)),"seal needs two available hands");
        Reject(()=>Apply(New(),"fm-turn-start",context:new(MaintainBasketSeal:true)),"seal cannot exist without basket");
        var tampered=State(walls,"parede_2",999);
        Check(engine.ValidateStateChange(system,walls.Fields!,tampered.Fields!,true,false,false) is not null,"wall health remains protected from autosave tampering");
        Console.WriteLine("F&M barriers: individual wall/curtain HP, costs/actions, reaction anti-domains, start-turn exposure, basket seal, concentration, source replacement and protected ledger passed.");
    }
}
