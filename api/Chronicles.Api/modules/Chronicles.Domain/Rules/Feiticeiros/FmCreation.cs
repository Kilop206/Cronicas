namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static readonly int[] FixedAttributes=[15,14,13,12,10,8];
    private static readonly Dictionary<int,int> PointCosts=new(){[8]=-2,[9]=-1,[10]=0,[11]=2,[12]=3,[13]=4,[14]=5,[15]=7};
    private static string StartAttributes(RuleState s,string method)
    {
        RuleState.Require(s.Get("inicializado")==0&&s.Get("criacao_metodo")==0,"A criação de atributos já começou ou os recursos iniciais já foram aplicados.");
        var values=method=="fixos"?FixedAttributes.ToArray():Enumerable.Repeat(10,6).ToArray();
        var mode=method=="fixos"?1:method=="pontos"?2:3;
        if(mode==3)
        {
            for(var i=0;i<6;i++)
            {
                s.Pool(4,6);var roll=s.Rolls[^1];var minimum=Array.IndexOf(roll.Rolls,roll.Rolls.Min());var keep=Enumerable.Range(0,4).Where(k=>k!=minimum).ToArray();var total=keep.Sum(k=>roll.Rolls[k]);
                s.Rolls[^1]=roll with {Expression="4d6 (descarta o menor)",KeptIndexes=keep,Total=total};values[i]=total;s.Set("criacao_sorteio_"+i,total);
            }
        }
        s.Set("criacao_metodo",mode);FmCatalog.Rows(s,"criacao_atributos",FmCatalog.Attributes.Select((chave,i)=>new{chave,base_valor=values[i],bonus=0}).ToArray());
        return method=="pontos"?"Compra iniciada: base 10, orçamento de 17 pontos. Valores de 8 a 15; custos da tabela do livro, incluindo devolução por 8/9. Distribua os bônus da origem e conclua a criação.":"Valores gerados. Distribua-os entre os seis atributos na tabela, aplique os bônus de origem e use Concluir atributos de criação.";
    }
    private static string FinishAttributes(RuleState s)
    {
        RuleState.Require(s.Get("criacao_metodo")>0&&s.Get("criacao_concluida")==0&&s.Get("inicializado")==0,"Inicie a criação de atributos antes de concluir.");
        var rows=FmCatalog.Rows(s,"criacao_atributos");
        RuleState.Require(rows.Length==6&&rows.Select(r=>FmCatalog.Text(r,"chave")).Distinct().Count()==6&&rows.All(r=>FmCatalog.Attributes.Contains(FmCatalog.Text(r,"chave"))),"Preencha cada um dos seis atributos uma única vez.");
        var basic=rows.Select(r=>FmCatalog.Number(r,"base_valor",-1)).ToArray();var bonus=rows.ToDictionary(r=>FmCatalog.Text(r,"chave"),r=>FmCatalog.Number(r,"bonus",-1));
        RuleState.Require(bonus.Values.All(v=>v>=0),"Bônus de origem não podem ser negativos.");
        if(s.Get("criacao_metodo")==1)RuleState.Require(basic.Order().SequenceEqual(FixedAttributes.Order()),"Valores fixos devem ser exatamente 15, 14, 13, 12, 10 e 8.");
        if(s.Get("criacao_metodo")==2)RuleState.Require(basic.All(PointCosts.ContainsKey)&&basic.Sum(v=>PointCosts.GetValueOrDefault(v,999))<=17,"Compra de atributos excede 17 pontos ou usa valor fora de 8 a 15.");
        if(s.Get("criacao_metodo")==3)RuleState.Require(basic.Order().SequenceEqual(Enumerable.Range(0,6).Select(i=>s.Get("criacao_sorteio_"+i)).Order()),"Use exatamente os seis resultados registrados nas rolagens.");
        var origin=s.Text("origem");
        if(origin is "inato" or "derivado" or "hibrido" || origin=="herdado")
        {
            RuleState.Require(bonus.Values.Order().SequenceEqual(new[]{0,0,0,0,1,2}),"Esta origem concede +2 em um atributo e +1 em outro.");
            if(origin=="herdado")
            {
                var eligible=s.Text("heranca_cla") switch {"gojo"=>new[]{"inteligencia","sabedoria"},"inumaki"=>["inteligencia","presenca"],"kamo"=>["constituicao","sabedoria"],_=>FmCatalog.Attributes};
                RuleState.Require(bonus.Where(kv=>kv.Value>0).All(kv=>eligible.Contains(kv.Key)),"Bônus de atributos incompatíveis com a herança de clã.");
            }
        }
        if(origin=="sem_tecnica")RuleState.Require(bonus.Values.Sum()==4&&bonus.Values.All(v=>v<=3),"Sem Técnica distribui quatro pontos, no máximo três no mesmo atributo.");
        if(origin=="corpo_mutante")RuleState.Require(bonus.Values.Sum()==2,"Corpo Mutante distribui dois pontos de atributo.");
        if(origin=="restringido")RuleState.Require(bonus.Where(kv=>kv.Key is "forca" or "destreza" or "constituicao").All(kv=>kv.Value>=1)&&bonus.Where(kv=>kv.Key is not ("forca" or "destreza" or "constituicao")).All(kv=>kv.Value==0)&&bonus.Values.Sum()==5,"Restringido recebe +1 em cada atributo físico e mais dois pontos físicos.");
        foreach(var row in rows)
        {
            var key=FmCatalog.Text(row,"chave");var value=FmCatalog.Number(row,"base_valor")+bonus[key];var cap=origin=="restringido"&&key is "forca" or "destreza" or "constituicao"?30:20;
            RuleState.Require(value>=0&&value<=cap,"Atributo inicial excede seu limite natural.");s.Field(key,value);
        }
        s.Set("criacao_concluida",1);return "Atributos e bônus de origem conferidos e aplicados. Escolha treinamentos, aptidões e habilidades antes de aplicar os recursos iniciais.";
    }
}
