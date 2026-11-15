using System.Globalization;

namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static string Cast(RuleState s,RuleCommand c,bool release=false)
    {
        RuleState.Require(s.Text("origem") is not ("restringido" or "sem_tecnica"),"Essa origem não possui feitiços; use suas técnicas marciais ou Novo Estilo da Sombra.");
        RuleState.Require(s.Get("exaustao_tecnica")==0,"Técnica exausta após expansão de domínio.");
        var context=c.Jujutsu??new();var index=int.Parse(c.Option!,CultureInfo.InvariantCulture);var row=FmCatalog.Rows(s,"feiticos")[index];var level=FmCatalog.Number(row,"nivel");
        if(level==6)RuleState.Require(FmCatalog.Has(s,"tecnica_maxima")&&FmCatalog.SpellLevel(s)>=4&&FmConditions.Training(s,"feiticaria")==2&&s.Get("maxima_recarga")==0,"Técnica Máxima exige sua aptidão, maestria em Feitiçaria, acesso ao nível 4 e recarga concluída.");
        else RuleState.Require(level<=FmCatalog.SpellLevel(s),"Nível de feitiço ainda indisponível.");
        var type=FmCatalog.Text(row,"tipo");var dice=FmCatalog.Text(row,"dados");var action=FmCatalog.Text(row,"acao");
        RuleState.Require(type!="passivo","Feitiços passivos são registrados no máximo de energia; não podem ser conjurados.");
        var fundamentals=ValidateFundamentals(s,context,level,type,action,dice);var improvements=context.Improvements??[];
        if(!release&&context.Ritual!="none"&&PrepareRitual(s,c with{Jujutsu=context},row,index) is { } ritualMessage)return ritualMessage;
        var extraCost=FundamentalCost(s,fundamentals,level,context);
        if(!release&&context.Ritual=="none")
        {
            RuleState.Require(improvements.Length==0,"Melhorias exigem ritual.");
            UseAction(s,fundamentals.Contains("rapido")?action=="completa"?"comum":"bonus":action);
        }
        var reduction=FmCatalog.Number(row,"reducao")+(s.Get("dominio")>0&&s.Get("confronto")==0?s.N("apt_dominio"):0);
        var technician=FmCatalog.ClassLevel(s,"tecnica");var focus=s.Text("foco_tecnica");
        if(technician>=10&&focus=="economia")reduction+=2;
        var baseCost=FmCatalog.SpellCosts[level];if(technician==20&&level is >=1 and <=3)baseCost/=2;
        var cost=level==0||level==1&&technician>=10&&focus=="economia"?0:Math.Max(1,baseCost-reduction);
        Spend(s,cost+extraCost);MarkFundamentals(s,fundamentals,context);
        if(level==6){s.Set("maxima_recarga",6-FmCatalog.Training(s)/2);s.Set("maxima_usada_turno",1);}
        var duration=improvements.Contains("sustento")?"concentrada":FmCatalog.Text(row,"duracao");
        if(duration=="sustentada")s.Set("sustento_"+index,1);
        if(duration=="concentrada"){StopSustainedConcentration(s);s.Set("concentrando",1);s.Set("concentrado_"+index,1);}
        var critical=false;var hit=true;var attack="";
        if(type=="ataque")
        {
            var active=FmConditions.Active(s);var bonus=FmCatalog.Mod(s,s.Text("atributo_tecnica"))+FmCatalog.Level(s)/2+FmCatalog.Training(s)-s.Get("exaustao")-FmConditions.SoulPenalty(s)+(technician>=10&&focus=="refino"?FmCatalog.Training(s)/2:0)+(technician==20?5:0)+2*improvements.Count(v=>v=="precisao")+2*fundamentals.Count(v=>v=="preciso");
            bonus-=Math.Max(active.Contains("envenenado")?2:0,active.Contains("amedrontado")?3:active.Contains("abalado")?1:0);
            var natural=FmConditions.Roll(s,bonus);var total=s.Rolls[^1].Total;critical=natural==20;hit=natural!=1&&(critical||c.Amount==0||total>=c.Amount);attack=$"Ataque {total}: "+(hit?"acertou. ":"errou. ");
        }
        var damageStart=s.Rolls.Count;var amount=hit&&dice.Length>0?RollDamage(s,dice,critical):0;
        if(hit&&fundamentals.Contains("potente"))amount+=RerollDamageDice(s,damageStart,s.Rolls.Count,Math.Max(0,FmCatalog.Mod(s,context.SpellPowerAttribute)),true,false);
        var damaging=type is "ataque" or "resistencia" or "area";
        if(hit&&damaging)amount+=4*level*improvements.Count(v=>v=="dano");
        if(hit&&damaging&&technician>0)
        {
            var modifier=FmCatalog.Mod(s,s.Text("atributo_tecnica"));amount+=level==6?3*modifier+3*FmCatalog.Level(s):level==5?2*modifier+2*FmCatalog.Level(s):level==4?2*modifier+FmCatalog.Level(s):level==3?2*modifier:level>=1?modifier:0;
            if(technician>=10&&focus=="destruicao"&&DiceExpression.TryParse(dice,out var damageDice))amount+=damageDice!.Count*(critical?2:1)+FmCatalog.Training(s);
        }
        if(hit&&dice.Length>0)amount=Math.Max(0,amount-FmConditions.OutputPenalty(s));
        if(damaging&&dice.Length>0)CompleteCombatStrike(s,hit,critical,context.TargetEnemy);
        var paid=cost+extraCost+FmConditions.CostPenalty(s);var dc=FmCatalog.TechniqueDc(s)+2*improvements.Count(v=>v=="cd")+2*fundamentals.Count(v=>v=="cruel");
        var description=RitualDescription(improvements,level)+FundamentalDescription(context,fundamentals);
        if(type=="cura")
        {
            RuleState.Require(level>0,"Feitiços de nível 0 não curam.");
            return $"{FmCatalog.Text(row,"nome")}: {amount} {(FmCatalog.Has(s,"energia_reversa")?"de cura":"PV temporários (sem Energia Reversa)")} para distribuir ao alvo. {paid} PE pagos."+description;
        }
        return $"{FmCatalog.Text(row,"nome")}: {attack}{(dice.Length>0?$"{amount} de efeito rolado. ":"")}{paid} PE pagos."+(type is "resistencia" or "area"?$" O alvo resolve TR contra CD {dc}; sucesso reduz dano à metade (nível 0 anula).":context.SpellForcesSave?$" O alvo resolve TR contra CD {dc}.":"")+description;
    }
    private static string Domain(RuleState s,string kind)
    {
        RuleState.Require(s.Text("origem")!="restringido" && s.Get("dominio")==0 && s.Get("exaustao_tecnica")==0,"Domínio indisponível ou técnica exausta.");
        RuleState.Require(s.Get("maos_livres")==2 && s.Get("cesta_selo")==0 && s.Get("pode_falar")==1 && !FmConditions.Active(s).Contains("engasgando"),"Expandir exige duas mãos livres e capacidade de fala; o selo da Cesta ocupa as duas mãos.");
        var dom=s.N("apt_dominio"); var barrier=s.N("apt_barreira"); var level=FmCatalog.Level(s);
        RuleState.Require(level>=8 && dom>=1 && FmCatalog.Has(s,"expansao_incompleta"),"Exige Expansão Incompleta, DOM 1 e nível 8.");
        var mode=kind switch {"incompleta"=>1,"completa"=>2,"garantido"=>3,"sem_barreiras"=>4,_=>0};
        if(mode>=2) RuleState.Require(level>=10 && dom>=3 && barrier>=3 && FmCatalog.Has(s,"expansao_completa") && FmCatalog.Has(s,"tecnicas_barreira"),"Expansão Completa exige nível 10, DOM/BAR 3, Técnicas de Barreira e sua aptidão.");
        if(mode>=3) RuleState.Require(level>=14 && dom>=4 && barrier>=4 && FmConditions.Training(s,"feiticaria")>0 && FmCatalog.Has(s,"acerto_garantido"),"Acerto Garantido exige nível 14, DOM/BAR 4, Feitiçaria treinada e sua aptidão.");
        if(mode==4) RuleState.Require(level==20 && dom==5 && barrier==5 && FmConditions.Training(s,"feiticaria")==2 && FmCatalog.Has(s,"expansao_sem_barreiras"),"Sem Barreiras exige nível 20, DOM/BAR 5, maestria em Feitiçaria e sua aptidão.");
        UseAction(s,"comum"); Spend(s,mode==1?15:mode==2?20:25);
        s.Set("dominio",mode); s.Set("dominio_rodadas",(mode==1?1:3)+dom);
        return $"Domínio {kind.Replace('_',' ')} ativo por {s.Get("dominio_rodadas")} rodadas. Abertura permite ataques de oportunidade; resolva alvos, barreiras, confronto e efeitos conforme o perfil da expansão.";
    }
    private static string EndDomain(RuleState s)
    {
        RuleState.Require(s.Get("dominio")>0,"Nenhum domínio ativo."); var mode=s.Get("dominio"); s.Set("dominio",0); s.Set("dominio_rodadas",0); s.Set("confronto",0);
        s.Set("exaustao_tecnica",new[]{0,1,2,4,5}[mode]); return $"Domínio encerrado: técnica exausta por {s.Get("exaustao_tecnica")} rodadas.";
    }
    private static string DomainCheck(RuleState s)
    {
        RuleState.Require(s.N("apt_dominio")>=1,"Sem nível de aptidão em domínio.");
        var total=s.Roll("1d10"+Signed(FmCatalog.Level(s)/2+s.N("apt_dominio")+s.N("bonus_confronto")+(s.Get("dominio")>0?2:0)-FmConditions.OutputPenalty(s)));
        return $"Confronto de domínio: {total}. Compare DOM e bônus dos dois usuários antes do teste; diferença de 5 ou mais vence. Incompleta só pode contestar (p.185–186).";
    }
    private static string Reverse(RuleState s,int points)
    {
        RuleState.Require(FmCatalog.Has(s,"energia_reversa") && (FmCatalog.ClassLevel(s,"suporte")>=6 || FmCatalog.Level(s)>=8 && s.N("apt_controle")>=3 && FmConditions.Training(s,"feiticaria")>0),"Energia Reversa exige sua aptidão e pré-requisitos ou a concessão de Suporte 6.");
        var amplified=FmCatalog.Has(s,"cura_amplificada");
        if(amplified) RuleState.Require(FmCatalog.Level(s)>=12 && s.N("apt_reversa")>=3,"Cura Amplificada exige nível 12 e ER 3.");
        var cap=1+(amplified?Aptitude(s,"reversa"):Aptitude(s,"reversa")/2);
        RuleState.Require(points>=1 && points<=cap,"Quantidade de PER acima do limite por uso.");
        RuleState.Require(s.Text("natureza")!="maldicao","Maldições não recebem autocura de energia reversa.");
        UseAction(s,"comum"); Spend(s,2*points);
        var dice=2+(FmCatalog.Level(s)>=10?1:0)+(FmCatalog.Level(s)>=15?1:0)+(FmCatalog.Level(s)>=20?1:0);
        var healing=Math.Max(0,s.Pool(dice*points,amplified?8:6)+(amplified?2:1)*Math.Max(FmCatalog.Mod(s,"presenca"),FmCatalog.Mod(s,"sabedoria"))-FmConditions.OutputPenalty(s));
        // A hybrid converts its own healing to cursed energy and pays PE directly (p.34).
        HealPoints(s,Math.Max(0,healing)); return $"Energia Reversa: {healing} PV recuperados por {points} PER.";
    }
    private static string Unnatural(RuleState s)
    {
        RuleState.Require(s.Get("combate")==1 && s.Get("antinatural_dia")==0,"Energia Antinatural exige combate e só pode ser usada uma vez por dia.");
        UseAction(s,"bonus"); s.Mana+=2*FmCatalog.Training(s); s.Set("antinatural_dia",1); return "Energia Antinatural: dobro do treinamento recuperado. Confirme novo dia para liberar outro uso.";
    }
    private static string HybridHeal(RuleState s,int uses)
    {
        var tier=Math.Min(3,FmCatalog.Level(s)/4); var maximum=1+tier;
        RuleState.Require(uses>=1 && s.Get("vigor_usos")+uses<=maximum,"Usos de Vigor Maldito insuficientes."); UseAction(s,"bonus");
        var healing=uses*(5*(1+tier)+FmCatalog.Mod(s,"constituicao")); s.Set("vigor_usos",s.Get("vigor_usos")+uses); HealPoints(s,Math.Max(0,healing)); return $"Vigor Maldito: {healing} PV recuperados.";
    }
    private static string SupportHeal(RuleState s)
    {
        var level=FmCatalog.Levels(s).GetValueOrDefault("suporte"); var modifier=FmCatalog.Mod(s,s.Text("atributo_especializacao"));
        RuleState.Require(level>0 && s.Get("suporte_usos")<modifier,"Usos de Suporte em Combate insuficientes.");
        UseAction(s,"bonus"); var dice=level>=16?"6d10":level>=12?"6d8":level>=8?"3d12":level>=4?"2d12":"2d6";
        var healing=Math.Max(0,s.Roll(dice)+modifier-FmConditions.OutputPenalty(s)); s.Set("suporte_usos",s.Get("suporte_usos")+1); return $"Suporte em Combate: {healing} de cura para aplicar a uma criatura em alcance de toque.";
    }
}
