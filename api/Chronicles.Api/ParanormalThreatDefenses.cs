namespace Chronicles.Api;

// Mechanical sources are explicit. Neither damage types nor narrative descriptions imply a source.
public static class ParanormalThreatDefenses
{
    public static readonly string[] EffectSources = ["normal","cursed-item","ritual","incorporeal","fear-ritual"];
    public static bool CanAffect(ParanormalThreatSheet sheet,string source) => !sheet.Incorporeal || source is "cursed-item" or "ritual" or "incorporeal" or "fear-ritual";
    public static string? ConditionBlocked(ParanormalThreatSheet sheet,ThreatCommand command)
    {
        RuleState.Require(command.Source=="manual" || EffectSources.Contains(command.Source),"Escolha a origem do efeito da condição.");
        if(command.Source=="fear-ritual" && sheet.Kind=="paranormal")return "Origem Paranormal impede rituais de Medo; condição não aplicada.";
        if(command.Source!="manual" && !CanAffect(sheet,command.Source))return "Incorporeidade impede efeitos dessa origem; condição não aplicada.";
        return null;
    }
    public static int ProtectedDamage(ParanormalThreatSheet sheet,RuleState s,ThreatCommand command,out bool criticalPrevented,out bool sneakPrevented,out int? rolled)
    {
        criticalPrevented=false;sneakPrevented=false;rolled=null;
        var mayPreventCritical=command.Critical && (sheet.CriticalImmune || sheet.Fortification>0);
        if(mayPreventCritical)RuleState.Require(command.NormalAmount is >=0 and <=9999 && command.NormalAmount<=command.Amount && command.NormalAmount>=command.SneakDamage,"Informe o dano normal sem crítico, incluindo o dano furtivo, entre zero e o dano total. Esse valor é usado se a proteção impedir o crítico.");
        var checkFortification=sheet.Fortification>0 && (command.Critical&&!sheet.CriticalImmune || command.SneakDamage>0);
        if(checkFortification && sheet.Fortification<100)rolled=s.Die(100);
        var fortified=checkFortification && (sheet.Fortification==100 || rolled<=sheet.Fortification);
        criticalPrevented=command.Critical && (sheet.CriticalImmune || fortified);
        sneakPrevented=command.SneakDamage>0 && fortified;
        return (criticalPrevented?command.NormalAmount!.Value:command.Amount)-(sneakPrevented?command.SneakDamage:0);
    }
}
