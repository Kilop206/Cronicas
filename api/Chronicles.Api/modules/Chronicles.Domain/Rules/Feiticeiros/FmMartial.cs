namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static readonly string[] FighterManeuvers = ["ajuste", "comando", "desarme", "esquiva", "trabalho_pes"];
    private static int PreparationMaximum(RuleState s) => FmCatalog.ClassLevel(s,"combate")==0?0:Math.Max(0,FmCatalog.EffectLevel(s,"combate")+FmCatalog.Mod(s,"sabedoria"));
    // Store spent points so a zero reserve is never confused with an absent/default value.
    private static int Preparation(RuleState s) => Math.Max(0,PreparationMaximum(s)-s.Get("preparo_gasto"));
    private static void SpendPreparation(RuleState s,int amount)
    {
        RuleState.Require(FmCatalog.ClassLevel(s,"combate")>0&&Preparation(s)>=amount,"Pontos de Preparo insuficientes.");
        s.Set("preparo_gasto",s.Get("preparo_gasto")+amount);
    }
    private static string RecoverPreparation(RuleState s,string reason)
    {
        RuleState.Require(FmCatalog.ClassLevel(s,"combate")>0,"Preparo exige Especialista em Combate.");
        if(reason=="analisar")UseAction(s,"comum");
        s.Set("preparo_gasto",Math.Max(0,s.Get("preparo_gasto")-(reason=="analisar"?2:1)));
        return $"Preparo: {Preparation(s)}/{PreparationMaximum(s)}. "+(reason=="eliminacao"?"Eliminação confirmada pela mesa.":"Campo de batalha analisado.");
    }
    private static string ExcitementDice(RuleState s)
    {
        var level=Math.Clamp(s.Get("empolgacao",1),1,5);
        var maximum=FmCatalog.ClassLevel(s,"lutador")>=11;
        return (maximum?new[]{"","2d4","2d6","2d8","3d6"}:new[]{"","1d4","1d6","2d4","2d6"})[level-1];
    }
    private static void UseManeuver(RuleState s,string maneuver)
    {
        RuleState.Require(FmCatalog.ClassLevel(s,"lutador")>0&&s.Get("combate")==1,"Manobras de Empolgação exigem Lutador em combate.");
        RuleState.Require(s.Fields.TryGetValue("manobras_lutador",out var list)&&list.ValueKind==System.Text.Json.JsonValueKind.Array&&list.EnumerateArray().Any(v=>v.ValueKind==System.Text.Json.JsonValueKind.String&&v.GetString()==maneuver),"Escolha uma manobra de Empolgação aprendida.");
        RuleState.Require(s.Get("manobra_"+maneuver)==0,"Esta manobra já foi usada nesta rodada.");
        s.Set("manobra_"+maneuver,1);
    }
    private static int RollExcitement(RuleState s,bool critical=false)
    {
        var expression=ExcitementDice(s);
        return expression.Length==0?0:RollDamage(s,expression,critical);
    }
    private static string FighterReaction(RuleState s,string maneuver)
    {
        UseManeuver(s,maneuver);
        if(maneuver=="esquiva")
        {
            UseAction(s,"reacao");var reduction=Math.Max(0,RollExcitement(s)+FmCatalog.Mod(s,"destreza")-FmConditions.OutputPenalty(s));
            return $"Esquiva: reduza em {reduction} o dano do ataque corpo a corpo recebido. Informe o dano restante em Receber dano.";
        }
        UseAction(s,"bonus");s.Set("defesa_empolgacao",Math.Max(0,RollExcitement(s)-FmConditions.OutputPenalty(s)));
        return $"Trabalho de Pés: +{s.Get("defesa_empolgacao")} de Defesa até o começo do próximo turno.";
    }
    private static string ConfirmManeuverHit(RuleState s)
    {
        RuleState.Require(FmCatalog.ClassLevel(s,"lutador")>0&&s.Get("turno_aberto")==1,"Confirme a manobra acertada durante seu turno de Lutador.");
        s.Set("empolgacao_acerto",1);return "Acerto de manobra externa confirmado para a Empolgação do próximo turno. A ação e o teste devem ser resolvidos pela mesa.";
    }
    private static void StartMartialTurn(RuleState s,bool beginningCombat)
    {
        foreach(var key in FighterManeuvers.Select(v=>"manobra_"+v).Concat(new[]{"defesa_empolgacao","defesa_descendente","corpo_treinado_pronto","lutador_superior_usado","ataque_furtivo_usado","ultimo_corpo_a_corpo","especial_preciso","fund_duplicado","fund_rapido"}))s.Set(key,0);
        if(FmCatalog.ClassLevel(s,"lutador")>0)
        {
            var initial=FmCatalog.ClassLevel(s,"lutador")==20?2:1;
            s.Set("empolgacao",beginningCombat?initial:Math.Clamp(s.Get("empolgacao",initial)+(s.Get("empolgacao_acerto")>0?1:-1),1,5));
        }
        s.Set("empolgacao_acerto",0);
    }
    private static void MartialAttackAction(RuleState s,FmActionContext context,string rowAction,bool fullAction=false)
    {
        RuleState.Require(context.UnarmedAction is "normal" or "bonus" or "superior","Ação desarmada inválida.");
        if(context.UnarmedAction=="normal") { UseAction(s,fullAction?"completa":rowAction);return; }
        RuleState.Require(context.AttackKind=="unarmed"&&s.Get("turno_aberto")==1&&FmCatalog.ClassLevel(s,"lutador")>0,"Este ataque exige Lutador desarmado no próprio turno.");
        if(context.UnarmedAction=="bonus")
        {
            RuleState.Require(s.Get("corpo_treinado_pronto")>0,"Corpo Treinado exige ataque desarmado ou marcial anterior neste turno.");
            UseAction(s,fullAction?"completa":"bonus");s.Set("corpo_treinado_pronto",0);
        }
        else
        {
            RuleState.Require(FmCatalog.ClassLevel(s,"lutador")==20&&s.Get("lutador_superior_usado")==0&&!FmConditions.Incapacitated(s),"Lutador Superior exige nível 20 e apenas um uso por rodada.");
            Spend(s,2);s.Set("lutador_superior_usado",1);
            if(fullAction)UseAction(s,"completa");
        }
    }
    private static int BeginCombatArt(RuleState s,FmActionContext context,bool melee)
    {
        var art=context.CombatArt;
        RuleState.Require(art is "none" or "arremesso_agil" or "distracao_letal" or "execucao_silenciosa" or "golpe_descendente" or "investida_imediata","Arte de combate inválida.");
        if(art=="none")return 0;
        RuleState.Require(s.Get("turno_aberto")==1,"Artes do Combate exigem seu turno.");
        RuleState.Require(art is not ("golpe_descendente" or "investida_imediata")||melee,"Esta arte exige ataque corpo a corpo.");
        RuleState.Require(art!="execucao_silenciosa"||context.TargetOffguard,"Execução Silenciosa exige alvo Desprevenido.");
        RuleState.Require(art!="arremesso_agil"||context.AttackKind=="thrown"&&s.Get("ultimo_corpo_a_corpo")>0,"Arremesso Ágil exige atacar um segundo alvo com arremesso após o ataque corpo a corpo.");
        SpendPreparation(s,art=="investida_imediata"?2:1);
        return art=="execucao_silenciosa"?Math.Max(1,1+FmCatalog.Mod(s,"sabedoria")/2):0;
    }
}
