namespace Chronicles.Api;

public static class Dnd2014Rage
{
    public static bool Active(RuleState s) => s.Get("furia_rodadas")>0;
    private static bool Unconscious(RuleState s) => s.Health==0 || s.Get("morto")>0 || Dnd2014Conditions.Has(s,"inconsciente");
    public static bool Benefits(RuleState s) => Active(s) && s.Get("furia_armadura_pesada")==0 && !Unconscious(s);
    public static int Bonus(RuleState s) => Benefits(s)?s.Get("nivel_barbaro")>=16?4:s.Get("nivel_barbaro")>=9?3:2:0;
    public static RuleAction[] Actions(SystemDefinition system,RuleState s)
    {
        if(!Dnd2014Damage.Supports(system)) return [];
        var result=new List<RuleAction>{new("combat-turn-start","Iniciar turno (liberar Surto)"),new("combat-turn-end","Concluir turno (avançar Fúria)"),new("combat-extra-turn-end","Concluir turno extra (sem avançar duração)")};
        if(s.Get("nivel_barbaro")>0)
        {
            result.Add(new("rage-start","Entrar em Fúria",null,[new("ready","Sem armadura pesada"),new("heavy","Com armadura pesada; sem benefícios"),..(s.Get("nivel_barbaro")>=11?new RuleChoice[]{new("ready-relentless","Sem armadura pesada; tentar Fúria Implacável automaticamente"),new("heavy-relentless","Armadura pesada; tentar Fúria Implacável automaticamente")}:[])]));
            result.Add(new("rage-end","Encerrar Fúria voluntariamente"));
            result.Add(new("rage-hostile-attack","Confirmar ataque contra criatura hostil (inclui erro ou agarrão)"));
            result.Add(new("rage-armor","Atualizar armadura durante a Fúria",null,[new("ready","Sem armadura pesada"),new("heavy","Com armadura pesada")]));
        }
        return result.ToArray();
    }
    public static bool Handles(string action) => action is "rage-start" or "rage-end" or "rage-hostile-attack" or "rage-armor" or "combat-turn-start" or "combat-turn-end" or "combat-extra-turn-end";
    public static DndCombatView View(RuleState s) => new(Dnd2014Damage.Types,Active(s),s.Get("furia_rodadas"),Bonus(s),s.Get("furia_armadura_pesada")>0,10+5*s.Get("furia_implacavel_usos"));
    public static void End(RuleState s) { foreach(var key in new[]{"furia_rodadas","furia_atividade","furia_implacavel_auto","furia_armadura_pesada"}) s.Set(key,0); }
    public static string Apply(RuleState s,RuleCommand command)
    {
        switch(command.Action)
        {
            case "rage-start":
                RuleState.Require(s.Get("nivel_barbaro")>0 && !Active(s) && !Dnd2014Conditions.Incapacitated(s),"Entrar em Fúria exige bárbaro consciente, capaz de agir e sem Fúria ativa.");
                Dnd2014ClassResources.Spend(s,"furia",1);s.Set("furia_rodadas",10);s.Set("furia_armadura_pesada",command.Option!.StartsWith("heavy",StringComparison.Ordinal)?1:0);s.Set("furia_implacavel_auto",command.Option.EndsWith("relentless",StringComparison.Ordinal)?1:0);s.Set("furia_atividade",s.Get("turno_atividade"));s.Set("concentrando",0);
                return "Fúria iniciada por um uso e uma ação bônus confirmada. Concentração encerrada; conclua cada turno para controlar a duração de até 1 minuto. Ataques contra criaturas hostis devem ser confirmados.";
            case "rage-end": End(s);return "Fúria encerrada voluntariamente; a ação bônus deve estar disponível.";
            case "rage-hostile-attack": MarkHostile(s);return "Ataque contra criatura hostil confirmado, mesmo se não causou dano ou ocorreu antes de entrar em Fúria.";
            case "rage-armor": RuleState.Require(Active(s),"Nenhuma Fúria ativa.");s.Set("furia_armadura_pesada",command.Option=="heavy"?1:0);return "Armadura atualizada; a Fúria só concede benefícios sem armadura pesada.";
            case "combat-turn-start":s.Set("surto_turno",0);return "Novo turno confirmado; Surto liberado. A duração da Fúria avança ao concluir o turno.";
            case "combat-turn-end":
            case "combat-extra-turn-end":
                var activity=s.Get("furia_atividade")>0||s.Get("turno_atividade")>0;s.Set("turno_atividade",0);
                if(!Active(s)) return "Turno concluído.";
                if(Unconscious(s)||command.Action=="combat-turn-end"&&s.Get("furia_rodadas")==1||s.Get("nivel_barbaro")<15&&!activity){End(s);return "Turno concluído; Fúria encerrada por duração, inconsciência ou ausência de ataque hostil/dano recebido.";}
                if(command.Action=="combat-turn-end")s.Set("furia_rodadas",s.Get("furia_rodadas")-1);
                s.Set("furia_atividade",0);return $"Turno concluído; Fúria com até {s.Get("furia_rodadas")} turnos normais restantes.";
            default:throw new ArgumentException("Ação de Fúria desconhecida.");
        }
    }
    public static void MarkHostile(RuleState s){s.Set("turno_atividade",1);if(Active(s))s.Set("furia_atividade",1);}
    public static void BeforeCommand(RuleState s,RuleCommand command)
    {
        var casting=command.Action is "cast" or "pact-cast" or "concentrate" || command.Action=="class-spend" && command.Option?.StartsWith("arcana_",StringComparison.Ordinal)==true;
        if(casting){RuleState.Require(!Dnd2014Conditions.Incapacitated(s),"Incapacitado não pode conjurar nem manter concentração.");RuleState.Require(!Active(s),"Durante a Fúria não é possível conjurar nem manter concentração.");}
    }
    public static string Damage(RuleState s,int amount,int previousHealth)
    {
        if(amount>0)s.Set("turno_atividade",1);
        if(!Active(s)||amount==0) return "";
        s.Set("furia_atividade",1);var message="";
        if(previousHealth>0 && s.Health==0 && s.Get("morto")==0 && s.Get("nivel_barbaro")>=11 && s.Get("furia_implacavel_auto")==1)
        {
            var dc=10+5*s.Get("furia_implacavel_usos");RuleState.Require(dc<=9999,"Fúria Implacável fora dos limites; conclua um descanso antes de tentar novamente.");
            s.Set("furia_implacavel_usos",s.Get("furia_implacavel_usos")+1);
            var bonus=s.N("res_constituicao");
            var expression=Dnd2014Conditions.Expression(s,"1d20"+(bonus<0?bonus.ToString():"+"+bonus),"save",s.Get("modo_testes"));
            var result=s.Roll(expression);if(result>=dc)s.Health=1;
            message=$" Fúria Implacável: {result} / CD {dc}; {(result>=dc?"permanece com 1 PV":"falhou")}.";
        }
        if(Unconscious(s))End(s);
        return message;
    }
    public static void AfterCommand(RuleState s){if(Active(s)&&Unconscious(s))End(s);}
    public static string? Validate(RuleState s)
    {
        if(s.Get("furia_rodadas") is <0 or >10 || s.Get("furia_implacavel_usos") is <0 or >1998) return "Duração de Fúria ou progressão de Fúria Implacável inválida.";
        if(new[]{"furia_atividade","furia_armadura_pesada","furia_implacavel_auto","turno_atividade"}.Any(key=>s.Get(key) is <0 or >1))return "Indicador de combate inválido.";
        return null;
    }
}
