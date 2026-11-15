using System.Globalization;
using System.Text.Json;

namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static readonly string[] RitualImprovements=["dano","precisao","cd","alcance","alvos","sustento","area","efeito"];
    private static string? PrepareRitual(RuleState s,RuleCommand c,JsonElement spell,int index)
    {
        var context=c.Jujutsu!;var action=FmCatalog.Text(spell,"acao");var level=FmCatalog.Number(spell,"nivel");var improvements=context.Improvements??[];
        RuleState.Require(context.Ritual is "simple" or "extended" && context.RitualFailure is "cancel" or "delay","Modo de ritual inválido.");
        RuleState.Require(s.Get("ritual_spell")==0,"Conclua ou cancele o ritual preparado antes de começar outro.");
        RuleState.Require(s.Get("combate")==0||s.Get("ritual_usado")==0,"Só é possível iniciar um ritual por turno.");
        RuleState.Require(action!="reacao","Feitiços de reação não podem ser usados em ritual.");
        var maximum=context.Ritual=="extended"?5:action=="bonus"?1:action=="comum"?2:0;
        RuleState.Require(improvements.Length>=1 && improvements.Length<=maximum && improvements.All(RitualImprovements.Contains),"Número ou tipo de melhorias incompatível com a ação do ritual.");
        foreach(var group in improvements.GroupBy(v=>v))RuleState.Require(group.Count()<=(group.Key is "dano" or "precisao" or "cd" or "area"?2:1),"Uma melhoria excede seu limite de repetições.");
        RuleState.Require(!improvements.Contains("sustento") || FmCatalog.Text(spell,"duracao")=="sustentada","Conversão de Sustento exige um feitiço sustentado.");
        if(s.Get("combate")==1)s.Set("ritual_usado",1);
        if(context.Ritual=="extended")
        {
            UseAction(s,"completa");UseAction(s,"movimento");StopSustainedConcentration(s);s.Set("concentrando",1);
            StoreRitual(s,index,2,improvements,context);
            return "Ritual estendido iniciado: ação completa e movimento gastos. Mantenha concentração até o próximo turno; ficará Desprevenido. Depois use Concluir ritual preparado (ação completa).";
        }
        RuleState.Require(FmConditions.Training(s,"prestidigitacao")>0,"O teste de Prestidigitação do ritual exige treinamento.");
        UseAction(s,action=="bonus"?"comum":"completa");
        var check=FmCatalog.Skills.Single(v=>v.Id=="prestidigitacao");var bonus=FmConditions.Bonus(s,check)-(FmConditions.Active(s).Contains("sofrendo")?5:0);
        FmConditions.Roll(s,bonus);var dc=10+(level==0?1:2*level)+2*improvements.Length;
        if(s.Rolls[^1].Total>=dc)return null;
        if(context.RitualFailure=="delay")
        {
            StoreRitual(s,index,1,improvements,context);return $"Ritual falhou contra CD {dc}: ação gasta; a conjuração será concluída no começo do próximo turno, com o custo pago na conclusão.";
        }
        return $"Ritual falhou contra CD {dc}: conjuração cancelada, ação gasta; nenhum PE foi consumido.";
    }
    private static void StoreRitual(RuleState s,int index,int mode,string[] improvements,FmActionContext context)
    {
        s.Set("ritual_spell",index+1);s.Set("ritual_mode",mode);s.Set("ritual_pronto",0);
        foreach(var key in RitualImprovements)s.Set("ritual_"+key,improvements.Count(v=>v==key));
        foreach(var key in FmClassChoices.Fundamentals)s.Set("ritual_fund_"+key,(context.Fundamentals??[]).Count(v=>v==key));
        s.Set("ritual_potente_sab",context.SpellPowerAttribute=="sabedoria"?1:0);s.Set("ritual_singular",context.SpellSingleTarget?1:0);
        s.Set("ritual_corpo_a_corpo",context.SpellMelee?1:0);s.Set("ritual_tr",context.SpellForcesSave?1:0);s.Set("ritual_inimigo",context.TargetEnemy?1:0);
        s.Set("ritual_adepto",context.UseAdeptDiscount?1:0);
    }
    private static string CancelRitual(RuleState s)
    {
        if(s.Get("ritual_mode")==2)s.Set("concentrando",0);
        foreach(var key in new[]{"ritual_spell","ritual_mode","ritual_pronto","ritual_potente_sab","ritual_singular","ritual_corpo_a_corpo","ritual_tr","ritual_inimigo","ritual_adepto"}.Concat(RitualImprovements.Select(v=>"ritual_"+v)).Concat(FmClassChoices.Fundamentals.Select(v=>"ritual_fund_"+v)))s.Set(key,0);
        return "Preparação de ritual encerrada; ações já gastas não são recuperadas.";
    }
    private static string FinishRitual(RuleState s,RuleCommand c)
    {
        RuleState.Require(s.Get("ritual_spell")>0 && s.Get("ritual_pronto")==1,"Aguarde o começo do próximo turno antes de concluir o ritual.");
        if(s.Get("ritual_mode")==2)
        {RuleState.Require(s.Get("concentrando")==1,"A concentração do ritual foi perdida.");UseAction(s,"completa");}
        var index=s.Get("ritual_spell")-1;
        var improvements=RitualImprovements.SelectMany(key=>Enumerable.Repeat(key,s.Get("ritual_"+key))).ToArray();
        var context=new FmActionContext(Ritual:"resolved",Improvements:improvements,
            Fundamentals:FmClassChoices.Fundamentals.SelectMany(key=>Enumerable.Repeat(key,s.Get("ritual_fund_"+key))).ToArray(),
            SpellPowerAttribute:s.Get("ritual_potente_sab")>0?"sabedoria":"inteligencia",SpellSingleTarget:s.Get("ritual_singular")>0,
            SpellMelee:s.Get("ritual_corpo_a_corpo")>0,SpellForcesSave:s.Get("ritual_tr")>0,TargetEnemy:s.Get("ritual_inimigo")>0,UseAdeptDiscount:s.Get("ritual_adepto")>0);
        CancelRitual(s);
        return Cast(s,new("fm-cast",c.Amount,index.ToString(CultureInfo.InvariantCulture),Jujutsu:context),true);
    }
    private static string RitualDescription(string[] improvements,int level)
    {
        if(improvements.Length==0)return "";
        var messages=new List<string>();
        var range=improvements.Count(v=>v=="alcance");if(range>0)messages.Add($"alcance +{1.5m*level} m");
        var area=improvements.Count(v=>v=="area");if(area>0)messages.Add($"área +{1.5m*area} m (linha: +{4.5m*area} m)");
        if(improvements.Contains("alvos"))messages.Add($"até {2+level} criaturas com sucesso garantido no TR");
        if(improvements.Contains("efeito"))messages.Add($"escolha um benefício numérico: +{level} imediato ou +{level/2} duradouro/sustentado; dados seguem a tabela de níveis de dados");
        return messages.Count==0?"":" Ritual: "+string.Join("; ",messages)+".";
    }
}
