using System.Globalization;

namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static int BarrierHealth(RuleState s)
    {
        RuleState.Require(FmCatalog.Has(s,"tecnicas_barreira")&&s.N("apt_barreira")>=1,"Técnicas de Barreira exige BAR 1.");
        var enhanced=FmCatalog.Has(s,"paredes_resistentes");
        if(enhanced)RuleState.Require(s.N("apt_barreira")>=2&&FmCatalog.Level(s)>=4,"Paredes Resistentes exige BAR 2 e nível 4.");
        return enhanced?10+s.N("apt_barreira")*FmCatalog.Level(s):5+s.N("apt_barreira")*(FmCatalog.Level(s)/2);
    }
    private static string BarrierAction(RuleState s)
    {
        if(!FmCatalog.Has(s,"barreira_rapida"))return "comum";
        RuleState.Require(s.N("apt_barreira")>=3&&FmCatalog.Level(s)>=6,"Barreira Rápida exige BAR 3 e nível 6.");return "bonus";
    }
    private static bool IsBarrierKey(string key)=>new[]{"parede_","cortina_"}.Any(prefix=>key.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(key.AsSpan(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out var id)&&id is >=1 and <=100);
    private static RuleChoice[] ActiveBarriers(RuleState s)=>s.State.Where(v=>IsBarrierKey(v.Key)&&v.Value>0).OrderBy(v=>v.Key,StringComparer.Ordinal).Select(v=>new RuleChoice(v.Key,$"{(v.Key.StartsWith("parede_",StringComparison.Ordinal)?"Parede":"Cortina")} {v.Key[(v.Key.IndexOf('_')+1)..]} · {v.Value} PV")).ToArray();
    private static string CreateBarrier(RuleState s,string kind,int health)
    {
        // The existing v1 integer ledger is bounded; a rejected creation is never committed.
        RuleState.Require(s.State.Count<100,"O estado da ficha está cheio; encerre barreiras ou efeitos concluídos antes de criar outros.");
        var id=Enumerable.Range(1,100).First(n=>s.Get(kind+"_"+n)==0);var key=kind+"_"+id;s.Set(key,health);return key;
    }
    private static string ManipulateBarrier(RuleState s,string key)
    {
        RuleState.Require(IsBarrierKey(key)&&key.StartsWith("parede_",StringComparison.Ordinal)&&s.Get(key)>0,"Escolha uma parede ativa.");
        _=BarrierHealth(s);UseAction(s,BarrierAction(s));return $"{key}: ação de manipular/mover registrada, sem novo custo de PE. O narrador resolve o novo posicionamento sem sobreposição.";
    }
    private static string BarrierDamage(RuleState s,string key,int amount)
    {
        RuleState.Require(IsBarrierKey(key)&&s.Get(key)>0&&amount>0,"Escolha uma barreira ativa e informe o dano final maior que zero.");
        var remaining=Math.Max(0,s.Get(key)-amount);s.Set(key,remaining);
        return $"{key}: {amount} de dano final; "+(remaining==0?"barreira destruída.":$"{remaining} PV restantes.");
    }
    private static string DismissBarrier(RuleState s,string key)
    {
        RuleState.Require(IsBarrierKey(key)&&s.Get(key)>0,"Escolha uma barreira ativa.");s.Set(key,0);
        return $"{key}: fim da barreira confirmado pela mesa, sem recuperação do custo.";
    }
    private static string Curtain(RuleState s,int sections,string action)
    {
        RuleState.Require(FmCatalog.Has(s,"cortina"),"Aprenda Cortina.");var health=3*BarrierHealth(s);
        RuleState.Require(sections is >=1 and <=9999,"Informe pelo menos um trecho de 9 m de cobertura.");
        // Cortina's paragraph specifies no action category. The caller must explicitly supply the table's ruling.
        if(action!="livre")UseAction(s,action);else RuleState.Require(!FmConditions.Incapacitated(s)&&(s.Get("combate")==0||s.Get("turno_aberto")==1),"Crie a Cortina no seu turno, conforme a ação acordada com a mesa.");
        Spend(s,sections);var key=CreateBarrier(s,"cortina",health);
        return $"{key}: Cortina criada, {health} PV, {sections*9} m de cobertura por {sections+FmConditions.CostPenalty(s)} PE, sem manutenção. Ação {action} acordada com o narrador; geometria, ocultamento e condições de entrada/saída são resolvidos pela mesa.";
    }
    private static string Basket(RuleState s,string action)
    {
        RuleState.Require(FmCatalog.Has(s,"cesta_oca_vime")&&FmCatalog.Level(s)>=5&&s.N("apt_barreira")>=1,"Cesta Oca de Vime exige sua aptidão, nível 5 e BAR 1.");
        RuleState.Require(FmConditions.Training(s,"historia")==2||FmCatalog.Has(s,"cesta_epoca"),"Cesta Oca de Vime exige maestria em História ou origem na época de uso confirmada pelo narrador.");
        RuleState.Require(s.Get("cesta_durabilidade")==0,"Sua Cesta Oca de Vime já está ativa.");
        UseAction(s,action);Spend(s,3);StopSustainedConcentration(s);s.Set("concentrando",1);s.Set("cesta_durabilidade",s.N("apt_barreira")+1);
        return $"Cesta Oca de Vime: {s.Get("cesta_durabilidade")} de durabilidade, concentração e proteção pessoal contra acerto garantido. Não protege de efeitos ambientais. No início do próximo turno, escolha manter o selo com as duas mãos."+(action=="reacao"?" Reação à expansão confirmada pela mesa.":"");
    }
    private static string BasketLoss(RuleState s)
    {
        RuleState.Require(s.Get("cesta_durabilidade")>0,"Nenhuma Cesta Oca de Vime ativa.");s.Set("cesta_durabilidade",s.Get("cesta_durabilidade")-1);
        if(s.Get("cesta_durabilidade")==0){s.Set("cesta_selo",0);s.Set("concentrando",0);return "Cesta Oca de Vime quebrou: resolva imediatamente o acerto garantido, se aplicável.";}
        return $"Cesta Oca de Vime: {s.Get("cesta_durabilidade")} de durabilidade.";
    }
    private static string AntiDomainTurn(RuleState s,FmActionContext context)
    {
        RuleState.Require(!context.MaintainBasketSeal||s.Get("cesta_durabilidade")>0&&s.Get("maos_livres")==2,"Manter o selo exige Cesta ativa e duas mãos livres no início do turno.");
        s.Set("cesta_selo",context.MaintainBasketSeal?1:0);
        var notes=context.MaintainBasketSeal?"Selo da Cesta mantido: duas mãos ocupadas; somente falha de concentração pode reduzir sua durabilidade. ":"";
        if(!context.GuaranteedHitExposure)return notes;
        if(s.Get("simples_durabilidade")>0)return notes+AntiDomainLoss(s);
        if(s.Get("cesta_durabilidade")>0)return notes+(s.Get("cesta_selo")==1?"Acerto garantido neutralizado pelo selo mantido.":BasketLoss(s));
        return notes+"Exposição confirmada sem anti-domínio ativo: resolva o acerto garantido com a mesa.";
    }
}
