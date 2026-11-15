namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static readonly string[] SpecialStrikeOptions=["amplo","atroz","impactante","letal","longo","penetrante","preciso","sanguinario","lento","sacrificio","desfocado"];
    private static string StyleReaction(RuleState s,string attribute)
    {
        RuleState.Require(FmClassChoices.Style(s,"interceptador"),"Aprenda Estilo do Interceptador.");UseAction(s,"reacao");
        var reduction=Math.Max(0,s.Pool(1+FmClassChoices.StyleSteps(s,"interceptador"),10)+FmCatalog.Mod(s,attribute)-FmConditions.OutputPenalty(s));
        return $"Interceptador: reduza em {reduction} o dano do ataque recebido pelo aliado dentro do alcance da sua arma. Aplique o restante na ficha dele.";
    }
    private static string StyleProtect(RuleState s,string kind)
    {
        RuleState.Require(FmClassChoices.Style(s,"protetor"),"Aprenda Estilo do Protetor.");UseAction(s,"reacao");
        return kind=="ataque"?"Protetor: imponha desvantagem no ataque contra o aliado a até 1,5 m. A rolagem é resolvida na ficha do atacante.":"Protetor: conceda vantagem no TR do aliado a até 1,5 m. A rolagem é resolvida na ficha dele.";
    }
    private static (int Attack,int Damage,bool Massive) StyleAttack(RuleState s,FmActionContext c)
    {
        var accuracy=0;var damage=0;var weapon=c.AttackKind!="unarmed";
        if(weapon&&c.SingleWeaponFreeHand&&FmClassChoices.Style(s,"duelista")){var step=FmClassChoices.StyleSteps(s,"duelista");accuracy+=1+step/2;damage+=2+step;}
        if(c.AttackKind=="thrown"&&FmClassChoices.Style(s,"arremessador"))damage+=2+FmClassChoices.StyleSteps(s,"arremessador");
        if(c.AttackKind=="ranged"&&FmClassChoices.Style(s,"distante")){var step=FmClassChoices.StyleSteps(s,"distante");accuracy+=1+step/2;damage+=2+step;}
        if(weapon&&(c.DualWielding||c.AttackKind=="offhand")&&FmClassChoices.Style(s,"duplo"))damage+=1+FmClassChoices.StyleSteps(s,"duplo");
        var massive=weapon&&(c.WeaponTwoHanded||c.WeaponHeavy)&&FmClassChoices.Style(s,"massivo");if(massive)damage+=1+FmClassChoices.StyleSteps(s,"massivo");
        RuleState.Require(!(c.SingleWeaponFreeHand&&(c.DualWielding||c.AttackKind=="offhand"||c.WeaponTwoHanded)),"Uma arma com a outra mão livre não pode ser declarada junto de duas armas ou arma em duas mãos.");
        return(accuracy,damage,massive);
    }
    private static string[] BeginSpecialStrike(RuleState s,FmActionContext c)
    {
        var options=c.SpecialStrike??[];
        RuleState.Require(options.Length<=15&&options.All(SpecialStrikeOptions.Contains),"Opção de Golpe Especial inválida.");
        if(options.Length==0){RuleState.Require(!c.BoostSelfSufficient,"O reforço de Autossuficiente exige um Golpe Especial.");return options;}
        RuleState.Require(FmCatalog.ClassLevel(s,"combate")>=4,"Golpe Especial exige nível real 4 de Combate.");
        foreach(var group in options.GroupBy(v=>v))RuleState.Require(group.Count()<=(group.Key=="desfocado"?3:group.Key=="sanguinario"?2:1),"Opção repetida além do limite do Golpe Especial.");
        RuleState.Require(!options.Contains("sacrificio")||c.SacrificeDamageConfirmed,"Resolva os 15 de dano de Sacrifício com a mesa e confirme antes do ataque.");
        if(options.Contains("sanguinario")||options.Contains("impactante"))_ = FmClassChoices.CombatDc(s);
        var cost=options.Sum(key=>key switch{"amplo" or "letal" or "penetrante" or "sanguinario"=>2,"lento"=>-2,"sacrificio" or "desfocado"=>-1,"preciso"=>s.Get("especial_preciso")>0?2:1,_=>1});
        var superior=FmCatalog.ClassLevel(s,"combate")==20;
        RuleState.Require(!c.BoostSelfSufficient||superior&&s.Get("autossuficiente_reforco")==0,"O reforço para 6 PE de Autossuficiente exige Combate 20 e um uso por cena.");
        var temporary=superior?Math.Min(s.MaxMana/2,c.BoostSelfSufficient?6:3):0;
        // These temporary points belong exclusively to this strike and expire immediately; no refill of the ordinary pool.
        var total=Math.Max(1,cost)+FmConditions.CostPenalty(s);Spend(s,Math.Max(0,total-temporary),false);
        if(c.BoostSelfSufficient)s.Set("autossuficiente_reforco",1);
        if(options.Contains("preciso")&&s.Get("combate")>0)s.Set("especial_preciso",1);
        return options;
    }
    private static string SpecialStrikeDescription(RuleState s,string[] options,bool hit,bool massive)
    {
        var notes=new List<string>();
        if(massive)notes.Add("Massivo rerrola 1/2 nos dados da arma uma vez, usando o novo resultado");
        if(options.Length==0)return notes.Count==0?"":" "+string.Join("; ",notes)+".";
        if(options.Contains("amplo"))notes.Add("Amplo: resolva o ataque contra uma criatura adicional");
        if(options.Contains("longo"))notes.Add("Longo: alcance +1,5 m corpo a corpo ou +9 m à distância");
        if(options.Contains("sacrificio"))notes.Add("Sacrifício: os 15 de dano foram confirmados como resolvidos pela mesa");
        if(hit&&options.Contains("penetrante"))notes.Add($"Penetrante: ignore {FmCatalog.Level(s)/2} de RD do alvo");
        if(hit&&options.Contains("sanguinario"))notes.Add($"Sangramento {(options.Count(v=>v=="sanguinario")==2?"médio":"leve")}, CD {FmClassChoices.CombatDc(s)}; aplique no alvo");
        if(hit&&options.Contains("impactante"))notes.Add($"Impactante: empurre 1,5 m por 15 de dano efetivamente aplicado; TR de Fortitude CD {FmClassChoices.CombatDc(s)} reduz à metade");
        return notes.Count==0?"":" "+string.Join("; ",notes)+".";
    }
    private static void CompleteCombatStrike(RuleState s,bool hit,bool critical,bool enemy)
    {
        s.Set("alvo_derrotado_pendente",hit&&enemy&&FmCatalog.ClassLevel(s,"combate")>0?1:0);s.Set("renovacao_critico",0);
        if(hit&&critical&&enemy&&FmCatalog.ClassLevel(s,"combate")>=6){s.Mana++;s.Set("renovacao_critico",1);}
    }
    private static string ConfirmDefeatedTarget(RuleState s)
    {
        RuleState.Require(FmCatalog.ClassLevel(s,"combate")>0&&s.Get("alvo_derrotado_pendente")==1,"Confirme o alvo reduzido a 0 antes de realizar o próximo ataque nativo.");
        s.Set("alvo_derrotado_pendente",0);s.Set("preparo_gasto",Math.Max(0,s.Get("preparo_gasto")-1));
        var recovered=FmCatalog.ClassLevel(s,"combate")>=6&&s.Get("renovacao_critico")==0;
        if(recovered)s.Mana++;
        return "Alvo reduzido a 0 confirmado: 1 Preparo recuperado."+(recovered?" Renovação pelo Sangue: 1 PE recuperado.":s.Get("renovacao_critico")>0?" O crítico já concedeu Renovação; não se repete pelo mesmo ataque.":"");
    }
    private static int RerollDamageDice(RuleState s,int start,int end,int maximum,bool keepBest,bool lowOnly)
    {
        var selected=s.Rolls.Select((roll,index)=>(roll,index)).Where(v=>v.index>=start&&v.index<end)
            .SelectMany(v=>v.roll.KeptIndexes.Select(i=>(Value:v.roll.Rolls[i],Expression:v.roll.Expression)))
            .Where(v=>!lowOnly||v.Value<=2).OrderBy(v=>v.Value).Take(maximum).ToArray();
        var change=0;
        foreach(var die in selected)
        {
            RuleState.Require(DiceExpression.TryParse(die.Expression,out var expression)&&expression is not null,"Dado de dano inválido para rerrolagem.");
            var next=s.Die(expression!.Sides);change+=(keepBest?Math.Max(next,die.Value):next)-die.Value;
        }
        return change;
    }
}
