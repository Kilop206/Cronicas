namespace Chronicles.Api;

public sealed record ThreatPerceptionView(int MissChance, bool PerceivesOpponent, bool VisualCheckAvailable, bool ProtectsUnseen, string[] Applied, string? Warning);

// Context concerns one declared opponent, not every observer or every object in the scene.
public static class ParanormalThreatPerception
{
    public static string? ValidateContext(ThreatSensoryContext? context) => context is null ||
        context.Distance is <0 or >999 || context.Concealment is <0 or >2 ||
        context.Cause is not ("other" or "darkness" or "paranormal-darkness" or "invisibility" or "blindness")
        ? "Informe distância de 0 a 999m (ou deixe desconhecida), grau e causa de camuflagem válidos." : null;

    public static ThreatPerceptionView Resolve(ParanormalThreatSheet sheet,RuleState state)
    {
        var c=sheet.SensoryContext;var a=sheet.SensoryAbilities;
        var blind=D20Conditions.Has(state,"cego");
        var concealment=c.Active?c.Concealment:state.Get("camuflagem_alvo");
        var unseen=blind || concealment==2 || c.Active&&c.OpponentUnseen;
        var chance=unseen?50:concealment==1?20:0;
        var applied=new List<string>();
        var visual=!blind && (!unseen || c.Active&&c.Cause=="darkness"&&!c.OpponentUnseen&&concealment<2);
        var perceives=!unseen;var protects=false;
        bool InRange(decimal range) => c.Active && !c.Unavailable && c.Distance is {} distance && distance<=range;
        // Vision abilities do not cure physical blindness, reveal invisible targets or penetrate paranormal darkness.
        if(c.Active && !blind && c.Cause=="darkness" && (a.Darkvision&&InRange(18) || a.LowLightVision&&InRange(9)&&concealment<2&&!c.OpponentUnseen))
        {
            chance=0;visual=true;perceives=true;protects=true;
            applied.Add(a.Darkvision&&InRange(18)?"Visão no Escuro":"Visão na Penumbra");
        }
        if(c.Active && a.BlindsightRange>0 && InRange(a.BlindsightRange) && c.Cause is "darkness" or "paranormal-darkness" or "invisibility" or "blindness")
        {
            chance=0;perceives=true;protects=true;applied.Add("Percepção às Cegas");
        }
        if(a.Scent && InRange(9) && unseen)
        {
            chance=Math.Min(chance,20);perceives=true;protects=true;applied.Add("Faro");
        }
        if(state.Get("ignora_camuflagem")!=0){chance=0;applied.Add("Efeito confirmado ignora camuflagem");}
        var configured=a.Scent||a.BlindsightRange>0||a.LowLightVision||a.Darkvision;
        string? warning=c.Active&&configured&&c.Distance is null?"Distância desconhecida: benefícios de sentidos com alcance não foram aplicados.":null;
        return new(chance,perceives,visual,protects,applied.ToArray(),warning);
    }

    public static ThreatPerceptionView Prepare(ParanormalThreatSheet sheet,RuleState state)
    {
        var view=Resolve(sheet,state);
        if(sheet.SensoryContext.Active)
        {
            if(view.ProtectsUnseen)state.ConditionRelationExemptions.Add("cego:desprevenido");
            if(!view.PerceivesOpponent)state.ContextConditions.Add("desprevenido");
        }
        return view;
    }
}
