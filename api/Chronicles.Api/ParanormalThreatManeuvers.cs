namespace Chronicles.Api;

public sealed record ThreatManeuverInput(int ExpectedVersion,string Kind,Guid TargetId,string TargetKind,int TargetVersion,
    bool InReach = false,string Visibility = "gm",string TargetSize = "medium",bool TargetSizeConfirmed = false,
    string TargetAttribute = "forca",bool AgileWeaponConfirmed = false,int AttackerAdjustment = 0,int DefenderAdjustment = 0,
    string Source = "normal",bool UsePerceptionContext = false,bool TargetBonusIncludesSize = false,bool TargetImmune = false);
public sealed record ManeuverCombatant(RuleState State,string Attribute,int Bonus,string Size,bool IncludesSize,
    bool Immune = false,bool ProneImmune = false);
public sealed record ManeuverRound(int Attacker,int Defender);
public sealed record ThreatManeuverOutcome(string Outcome,int AttackerTotal,int DefenderTotal,int Ties,decimal Displacement,
    bool ProneApplied,int MissChance,int? ConcealmentRoll,ManeuverRound[] Rounds,string Message);

public static class ParanormalThreatManeuvers
{
    public static int SizeModifier(string size) => size switch {
        "tiny"=>-5,"small"=>-2,"medium"=>0,"large"=>2,"huge"=>5,"colossal"=>10,
        _=>throw new ArgumentException("Tamanho de manobra inválido.") };

    public static ManeuverCombatant Threat(ParanormalThreatSheet sheet,int health,int maximum,Func<int,int> draw,bool usePerception=false)
    {
        if(ParanormalThreatSchema.Validate(sheet) is {} error)throw new ArgumentException(error);
        RuleState.Require(sheet.ManeuverProfile.Configured,"Configure o bônus de Luta em manobras da ameaça; nenhum valor é inferido de armas ou descrições.");
        var p=sheet.ManeuverProfile;
        var source=usePerception?sheet:sheet with{SensoryContext=new(),State=sheet.State.Where(x=>x.Key is not("camuflagem_alvo" or "ignora_camuflagem")).ToDictionary()};
        var s=ParanormalThreatSchema.ToState(source,health,maximum,draw);
        if(usePerception)ParanormalThreatPerception.Prepare(source,s);
        return new(s,sheet.Incorporeal?"agilidade":p.Attribute,p.Bonus,sheet.Size,p.IncludesSize,sheet.ManeuverImmune,ParanormalThreatSchema.ImmuneCondition(sheet,"caido"));
    }
    public static ThreatManeuverOutcome Resolve(ManeuverCombatant attacker,ManeuverCombatant defender,ThreatManeuverInput input,int missChance=0,bool sourceBlocked=false)
    {
        RuleState.Require(input.Kind is "trip" or "push" && input.InReach,"Escolha Derrubar ou Empurrar e confirme o alcance corpo a corpo.");
        RuleState.Require(input.Visibility is "gm" or "private" or "campaign" && input.AttackerAdjustment is >=-999 and <=999 && input.DefenderAdjustment is >=-999 and <=999,"Privacidade ou modificadores de manobra inválidos.");
        RuleState.Require(ParanormalThreatDefenses.EffectSources.Contains(input.Source),"Origem da manobra inválida.");
        RuleState.Require(!D20Conditions.CannotAct(attacker.State),"As condições da ameaça impedem executar a manobra.");
        RuleState.Require(!D20Conditions.Has(defender.State,"inconsciente")&&!D20Conditions.Has(defender.State,"paralisado"),"Este alvo não pode executar a oposição física. Resolva suas exceções com o mestre; nenhuma vitória automática é presumida.");
        RuleState.Require(!D20Conditions.Has(attacker.State,"agarrado")||attacker.State.Get("arma_leve")==1,"Ameaça agarrada só ataca com arma leve; confirme o contexto.");
        RuleState.Require(!D20Conditions.Has(defender.State,"agarrado")||defender.State.Get("arma_leve")==1,"Alvo agarrado: confirme a arma leve antes da oposição.");
        RuleState.Require(missChance is 0 or 20 or 50,"Chance de falha inválida.");
        var attackBonus=attacker.Bonus+(attacker.IncludesSize?0:SizeModifier(attacker.Size))+input.AttackerAdjustment;
        var defenseBonus=defender.Bonus+(defender.IncludesSize?0:SizeModifier(defender.Size))+input.DefenderAdjustment;
        RuleState.Require(attackBonus is >=-9999 and <=9999 && defenseBonus is >=-9999 and <=9999,"Bônus final fora do limite de −9999 a 9999.");
        // Validate sizes even when a printed final modifier already includes their effect.
        SizeModifier(attacker.Size);SizeModifier(defender.Size);
        ThreatManeuverOutcome Result(string status,int a,int d,int ties,decimal distance,bool prone,int? concealment,ManeuverRound[] rounds,string message)=>new(status,a,d,ties,distance,prone,missChance,concealment,rounds,message);
        if(sourceBlocked||input.TargetImmune||defender.Immune||input.Kind=="trip"&&defender.ProneImmune)return Result("blocked",0,0,0,0,false,null,[],"A origem do efeito ou uma imunidade impede a manobra; nenhum dado foi rolado.");
        var attackSeverity=D20Conditions.Severity(attacker.State,attacker.Attribute,"per_luta",true,false,true);
        var defenseSeverity=D20Conditions.Severity(defender.State,defender.Attribute,"per_luta",true,false,true);
        var attackExpression=D20Checks.ParanormalExpression(attacker.State,attacker.Attribute,attackBonus,attackSeverity);
        var defenseExpression=D20Checks.ParanormalExpression(defender.State,defender.Attribute,defenseBonus,defenseSeverity);
        var rounds=new List<ManeuverRound>();
        for(int i=0;i<20;i++)
        {
            var a=attacker.State.Roll(attackExpression);var d=defender.State.Roll(defenseExpression);rounds.Add(new(a,d));
            if(a==d)continue;
            if(a<d)return Result("failed",a,d,i,0,false,null,rounds.ToArray(),$"O alvo venceu a oposição de Luta: {a} × {d}. Nenhum efeito aplicado.");
            int? camouflage=null;
            if(missChance>0){camouflage=attacker.State.Die(10);if(camouflage<=missChance/10)return Result("concealment",a,d,i,0,false,camouflage,rounds.ToArray(),$"A ameaça venceu {a} × {d}, mas falhou pela camuflagem ({missChance}%, d10 = {camouflage}).");}
            var distance=input.Kind=="push"?1.5m*(1+(a-d)/5):a-d>=5?1.5m:0;
            if(input.Kind=="trip")D20Conditions.Apply(defender.State,new("condition",Option:"caido"));
            return Result("success",a,d,i,distance,input.Kind=="trip",camouflage,rounds.ToArray(),input.Kind=="trip"?$"Alvo derrubado; condição Caído aplicada{(distance>0?" e deslocamento adicional de 1,5m":"")}. Não foi causado dano automático.":$"Alvo empurrado {distance}m. O mestre confirma a direção e a posição; acompanhar exige movimento disponível.");
        }
        return Result("tied",rounds[^1].Attacker,rounds[^1].Defender,20,0,false,null,rounds.ToArray(),"Vinte empates consecutivos: resultado não resolvido, sem efeito. Inicie outra oposição para continuar; nenhum vencedor foi escolhido.");
    }
}
