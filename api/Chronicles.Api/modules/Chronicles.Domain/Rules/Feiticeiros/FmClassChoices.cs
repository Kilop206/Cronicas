using System.Text.Json;

namespace Chronicles.Domain;

// Choices live in the existing aptidoes list so released v1 sheets can use the new mechanics.
public static class FmClassChoices
{
    public static readonly string[] Styles=["defensivo","arremessador","duelista","interceptador","protetor","distante","duplo","massivo"];
    public static readonly string[] Fundamentals=["cruel","distante","duplicado","expansivo","potente","preciso","rapido"];
    public static bool AdeptStyle(RuleState s,string key)=>FmCatalog.Has(s,"adepto_combate")&&FmCatalog.Has(s,"adepto_estilo_"+key);
    public static bool Style(RuleState s,string key)=>FmCatalog.ClassLevel(s,"combate")>0&&FmCatalog.Has(s,"estilo_"+key)||AdeptStyle(s,key);
    public static int StyleSteps(RuleState s,string key)=>Math.Min(4,(AdeptStyle(s,key)?FmCatalog.Level(s):FmCatalog.EffectLevel(s,"combate"))/4);
    public static int Defense(RuleState s)=>Style(s,"defensivo")?2+StyleSteps(s,"defensivo"):0;
    public static bool AdeptFundamental(RuleState s,string key)=>FmCatalog.Has(s,"adepto_feiticaria")&&FmCatalog.Has(s,"adepto_fundamento_"+key);
    public static bool Fundamental(RuleState s,string key)=>FmCatalog.ClassLevel(s,"tecnica")>0&&FmCatalog.Has(s,"fundamento_"+key)||AdeptFundamental(s,key);
    public static string CombatAttribute(RuleState s)
    {
        if(s.Text("especializacao")=="combate")return s.Text("atributo_especializacao");
        var selected=new[]{"forca","destreza","sabedoria"}.Where(key=>FmCatalog.Has(s,"chave_combate_"+key)).ToArray();
        RuleState.Require(selected.Length==1,"Escolha o atributo-chave de Combate na progressão da multiclasse.");return selected[0];
    }
    public static int CombatDc(RuleState s)=>10+FmCatalog.Level(s)/2+FmCatalog.Training(s)+FmCatalog.Mod(s,CombatAttribute(s))+s.N("bonus_cd")+FmCatalog.MartialDcBonus(s)-s.Get("exaustao");
    public static void Validate(RuleState s)
    {
        if(!s.Fields.TryGetValue("aptidoes",out var value))return;
        RuleState.Require(value.ValueKind==JsonValueKind.Array,"Lista de aptidões inválida.");
        var entries=value.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).ToArray();
        var combat=FmCatalog.ClassLevel(s,"combate");var technician=FmCatalog.ClassLevel(s,"tecnica");
        var styles=entries.Where(v=>Styles.Any(key=>v=="estilo_"+key)).ToArray();
        var fundamentals=entries.Where(v=>Fundamentals.Any(key=>v=="fundamento_"+key)).ToArray();
        RuleState.Require(!FmCatalog.Has(s,"acervo_amplo")||combat>=6,"Acervo Amplo exige Combate 6.");
        RuleState.Require(!FmCatalog.Has(s,"expansao_fundamentos")||technician>=8,"Expansão dos Fundamentos exige Técnica 8.");
        RuleState.Require(styles.Distinct().Count()==styles.Length&&styles.Length<=(combat>0?1+(combat>=6?1:0)+(combat>=12?1:0)+(FmCatalog.Has(s,"acervo_amplo")?1:0):0),"Orçamento de estilos excedido; confira os níveis reais 6/12 e Acervo Amplo.");
        RuleState.Require(fundamentals.Distinct().Count()==fundamentals.Length&&fundamentals.Length<=(technician>0?2+(technician>=12?1:0)+(FmCatalog.Has(s,"expansao_fundamentos")?1+(technician>=12?1:0):0):0),"Orçamento de Fundamentos excedido; confira Técnica 12 e Expansão dos Fundamentos.");
        RuleState.Require(!fundamentals.Contains("fundamento_rapido")||technician>=6,"Feitiço Rápido exige nível real 6 de Técnica.");
        if(FmCatalog.Has(s,"adepto_combate"))RuleState.Require(FmConditions.Training(s,"intuicao")==2,"Adepto de Combate exige maestria em Intuição.");
        if(FmCatalog.Has(s,"adepto_feiticaria"))RuleState.Require(FmConditions.Training(s,"feiticaria")==2&&s.Text("origem") is not ("sem_tecnica" or "restringido"),"Adepto de Feitiçaria exige maestria em Feitiçaria e possuir feitiços.");
        if(FmCatalog.Has(s,"adepto_combate")||FmCatalog.Has(s,"adepto_feiticaria"))RuleState.Require(entries.Where(v=>v.StartsWith("adepto_",StringComparison.Ordinal)&&!v.StartsWith("adepto_estilo_",StringComparison.Ordinal)&&!v.StartsWith("adepto_fundamento_",StringComparison.Ordinal)).Distinct().Count()<=3,"Os talentos Adepto registrados excedem o máximo de três.");
        var adeptStyles=entries.Where(v=>v.StartsWith("adepto_estilo_",StringComparison.Ordinal)).ToArray();
        RuleState.Require(adeptStyles.Length<=1&&adeptStyles.All(v=>FmCatalog.Has(s,"adepto_combate")&&Styles.Any(key=>v=="adepto_estilo_"+key)),"Escolha um estilo válido para Adepto de Combate.");
        var adeptFundamentals=entries.Where(v=>v.StartsWith("adepto_fundamento_",StringComparison.Ordinal)).ToArray();
        RuleState.Require(adeptFundamentals.Length<=1&&adeptFundamentals.All(v=>FmCatalog.Has(s,"adepto_feiticaria")&&Fundamentals.Where(key=>key!="rapido").Any(key=>v=="adepto_fundamento_"+key)),"Adepto de Feitiçaria concede uma mudança, exceto Rápido.");
        var keys=entries.Where(v=>v.StartsWith("chave_combate_",StringComparison.Ordinal)).ToArray();
        RuleState.Require(keys.Length<=1&&keys.All(v=>v is "chave_combate_forca" or "chave_combate_destreza" or "chave_combate_sabedoria"),"Atributo-chave adicional de Combate inválido ou repetido.");
    }
}
