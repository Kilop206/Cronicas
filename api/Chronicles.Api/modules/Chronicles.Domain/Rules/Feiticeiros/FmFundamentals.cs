namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static string[] ValidateFundamentals(RuleState s,FmActionContext context,int level,string type,string action,string dice)
    {
        var selected=context.Fundamentals??[];
        RuleState.Require(selected.Length<=9&&selected.All(FmClassChoices.Fundamentals.Contains),"Mudança de Fundamento inválida.");
        if(selected.Length==0)return selected;
        foreach(var group in selected.GroupBy(v=>v))
        {
            RuleState.Require(FmClassChoices.Fundamental(s,group.Key),"Aprenda a Mudança de Fundamento pela classe ou por Adepto de Feitiçaria antes de aplicá-la.");
            RuleState.Require(group.Count()<=(group.Key is "cruel" or "preciso"?2:1),"Mudança de Fundamento repetida além do limite.");
        }
        var damage=type is "ataque" or "resistencia" or "area" && dice.Length>0;
        RuleState.Require(!selected.Contains("cruel")||type is "resistencia" or "area"||context.SpellForcesSave,"Feitiço Cruel exige um TR do alvo.");
        RuleState.Require(!selected.Contains("preciso")||type=="ataque","Feitiço Preciso exige uma jogada de ataque.");
        RuleState.Require(!selected.Contains("expansivo")||type=="area","Feitiço Expansivo exige área.");
        RuleState.Require(!selected.Contains("duplicado")||damage&&context.SpellSingleTarget&&type!="area","Feitiço Duplicado exige dano contra uma única criatura.");
        RuleState.Require(!selected.Contains("potente")||damage,"Feitiço Potente exige dano.");
        RuleState.Require(context.SpellPowerAttribute is "inteligencia" or "sabedoria","Potente usa Inteligência ou Sabedoria.");
        if(selected.Contains("rapido"))
        {
            RuleState.Require(FmCatalog.ClassLevel(s,"tecnica")>=6&&action is "comum" or "completa","Feitiço Rápido exige Técnica 6 e ação comum ou completa.");
            RuleState.Require(context.Ritual=="none","A combinação de Rápido e ritual requer adjudicação da mesa; use conjuração normal nesta ação.");
        }
        // Maximum is a named tier, not an official numeric level 6. Do not invent its level-based surcharge.
        RuleState.Require(level<6||!selected.Any(v=>v is "rapido" or "duplicado"),"O custo por nível de Rápido/Duplicado para Técnica Máxima precisa de adjudicação; o índice interno 6 não é um nível definido pelo livro.");
        return selected;
    }
    private static int FundamentalCost(RuleState s,string[] selected,int level,FmActionContext context)
    {
        if(selected.Contains("duplicado"))RuleState.Require(s.Get("combate")==0||s.Get("fund_duplicado")==0,"Feitiço Duplicado já foi usado nesta rodada.");
        if(selected.Contains("rapido"))RuleState.Require(s.Get("combate")==0||s.Get("fund_rapido")==0,"Feitiço Rápido já foi usado nesta rodada.");
        if(context.UseAdeptDiscount)RuleState.Require(selected.Any(key=>FmClassChoices.AdeptFundamental(s,key))&&s.Get("adepto_feiticaria_usos")<FmCatalog.Training(s),"Desconto de Adepto exige usar a mudança concedida e possuir usos nesta cena.");
        return Math.Max(0,selected.Sum(key=>key switch{"cruel" or "preciso"=>1,"distante" or "potente"=>2,"expansivo"=>3,_=>2*Math.Max(1,level)})-(context.UseAdeptDiscount?1:0));
    }
    private static void MarkFundamentals(RuleState s,string[] selected,FmActionContext context)
    {
        if(context.UseAdeptDiscount)s.Set("adepto_feiticaria_usos",s.Get("adepto_feiticaria_usos")+1);
        if(s.Get("combate")==0)return;
        foreach(var key in new[]{"duplicado","rapido"})if(selected.Contains(key))s.Set("fund_"+key,1);
    }
    private static string FundamentalDescription(FmActionContext context,string[] selected)
    {
        var notes=new List<string>();
        if(selected.Contains("distante"))notes.Add(context.SpellMelee?"Distante: o feitiço corpo a corpo recebe alcance de 9 m":"Distante: alcance dobrado");
        if(selected.Contains("expansivo"))notes.Add("Expansivo: área ×1,5");
        if(selected.Contains("duplicado"))notes.Add("Duplicado: resolva o mesmo feitiço contra uma segunda criatura");
        if(selected.Contains("potente"))notes.Add("Potente: os menores dados de dano são rerrolados até o limite do atributo escolhido, mantendo os melhores resultados");
        return notes.Count==0?"":" "+string.Join("; ",notes)+".";
    }
}
