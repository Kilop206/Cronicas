namespace Chronicles.Api;

public sealed record ThreatCommand(string Action, string? Option = null, int Amount = 0, string Visibility = "gm",
    bool Half = false, bool IgnoreResistance = false, string Source = "normal", bool Critical = false, int? NormalAmount = null,
    Guid? TargetId = null, int? TargetVersion = null, int AdditionalCreatures = 0);
public sealed record ThreatAttackOutcome(int Index, int Total, int Natural, bool? Hit, bool Critical, int Damage, string DamageType, int ExtraDamage, string ExtraType);
public sealed record ThreatDamageResult(int Raw, int AfterSave, int Multiplier, int Reduction, int Applied, bool Immune, string[] Sources);
public sealed record ThreatResult(ParanormalThreatSheet Sheet,int Health,string Message,RuleRoll[] Rolls,ThreatAttackOutcome[]? Attacks=null,ThreatDamageResult? Damage=null);

public static class ParanormalThreatRules
{
    public static ThreatResult Apply(ParanormalThreatSheet sheet,int health,int maximum,ThreatCommand command,Func<int,int> draw)
    {
        if(ParanormalThreatSchema.Validate(sheet) is {} error) throw new ArgumentException(error);
        RuleState.Require(command.Amount is >=0 and <=9999 && command.Visibility is "gm" or "private" or "campaign", "Valor ou privacidade inválidos.");
        var s=ParanormalThreatSchema.ToState(sheet,health,maximum,draw);
        var attacks=sheet.Attacks.ToArray();var abilities=sheet.Abilities.ToArray();
        string message;
        void Spend(int cost) { if(cost>0) s.SpendMana(cost+D20Conditions.CostIncrease(s)); }
        ThreatResult Result(string text,ThreatAttackOutcome[]? outcomes=null,ThreatDamageResult? damage=null) => new(sheet with{Attacks=attacks,Abilities=abilities,Effort=s.Mana,State=new(s.State)},s.Health,text,s.Rolls.ToArray(),outcomes,damage);
        switch(command.Action)
        {
            case "attack":
                RuleState.Require(!D20Conditions.CannotAct(s),"As condições atuais impedem atacar.");
                var index=Array.FindIndex(attacks,a=>a.Id==command.Option);RuleState.Require(index>=0,"Ataque não encontrado.");
                var attack=attacks[index];
                RuleState.Require(!D20Conditions.Has(s,"agarrado") || s.Get("arma_leve")==1,"Agarrado só permite arma leve; confirme a arma no contexto.");
                RuleState.Require(attack.Ammunition==-1 || attack.Ammunition>=attack.Count,"Munição insuficiente para a sequência completa.");
                Spend(attack.Cost);var outcomes=new List<ThreatAttackOutcome>();
                for(int i=0;i<attack.Count;i++)
                {
                    var severity=D20Conditions.Severity(s,attack.Attribute,attack.Style=="ranged"?"per_pontaria":"per_luta",true,attack.Style=="ranged",true);
                    var expression=D20Checks.ParanormalExpression(s,attack.Attribute,attack.Bonus,severity);
                    var total=s.Roll(expression);var roll=s.Rolls[^1];var natural=roll.Rolls[roll.KeptIndexes[0]];
                    var hit=natural==20 || command.Amount==0 || total>=command.Amount;
                    var camouflage=s.Get("ignora_camuflagem")!=0?0:D20Conditions.Has(s,"cego")||s.Get("camuflagem_alvo")==2?5:s.Get("camuflagem_alvo")==1?2:0;
                    if(hit && camouflage>0 && s.Die(10)<=camouflage) hit=false;
                    var critical=hit && natural>=attack.Margin;
                    var damage=hit?Math.Max(0,DamageFormula.Evaluate(attack.Damage,s,critical?attack.Multiplier:1)):0;
                    var extra=hit?Math.Max(0,DamageFormula.Evaluate(attack.ExtraDamage,s)):0;
                    outcomes.Add(new(i+1,total,natural,command.Amount==0&&hit?null:hit,critical,damage,attack.DamageType,extra,attack.ExtraType));
                }
                if(attack.Ammunition>=0) attacks[index]=attack with{Ammunition=attack.Ammunition-attack.Count};
                return Result($"{attack.Name}: {attack.Count} ataque(s) resolvido(s), custo e munição registrados. {(command.Amount==0?"Confirme os acertos; Defesa não informada.":"Aplique cada acerto ao alvo separadamente, após as defesas dele.")}",outcomes.ToArray());
            case "check":
                var attribute=command.Option is not null && ParanormalThreatSchema.Attributes.Contains(command.Option)?command.Option:null;
                var skill=sheet.Skills.SingleOrDefault(x=>x.Id==command.Option);
                RuleState.Require(attribute is not null || skill is not null,"Teste não encontrado.");
                var key=attribute??"per_"+skill!.Id;
                if(skill?.Id=="reflexos" && D20Conditions.Has(s,"indefeso")) return Result("Reflexos: falha automática por indefeso.");
                if(skill?.Id=="percepcao" && (s.Get("sentido_percepcao")==1 && D20Conditions.Has(s,"cego") || s.Get("sentido_percepcao")==2 && D20Conditions.Has(s,"surdo"))) return Result("Percepção: sentido indisponível.");
                var check=D20Checks.ParanormalExpression(s,attribute??skill!.Attribute,skill?.Bonus??0,key:key);
                var checkedTotal=s.Roll(check);message=$"{skill?.Name??attribute}: {check} = {checkedTotal}";
                if(command.Amount>0) message+=$" / DT {command.Amount}: {(D20Checks.Passed(s,checkedTotal,command.Amount,true)?"sucesso":"falha")}";
                return Result(message+".");
            case "damage":
                var resolved=ResolveDamage(sheet,s,command);
                var before=s.Health;s.Health-=resolved.Applied;
                if(s.Health<before && (sheet.FastHealing.Except?.Contains(command.Option!)==true || command.Source is "hunger" or "thirst" or "suffocation")) s.Set("cura_impedida",Math.Min(maximum,s.Get("cura_impedida")+before-s.Health));
                return Result($"Dano {resolved.Raw} → {resolved.Applied}; {(resolved.Immune?"imunidade aplicada":$"multiplicador ×{resolved.Multiplier}, RD {resolved.Reduction}")}. {s.Health}/{maximum} PV.",damage:resolved);
            case "heal":
                var old=s.Health;s.Health+=command.Amount;s.Set("cura_impedida",Math.Max(0,s.Get("cura_impedida")-(s.Health-old)));return Result("Cura comum aplicada; não executa uma ressurreição nem remove condições automaticamente.");
            case "ability":
                var abilityIndex=Array.FindIndex(abilities,a=>a.Id==command.Option);RuleState.Require(abilityIndex>=0,"Habilidade não encontrada.");
                var ability=abilities[abilityIndex];RuleState.Require(!D20Conditions.CannotAct(s,physical:!ability.MentalOnly),"As condições atuais impedem usar esta ação.");RuleState.Require(ability.Action!="passive","Uma habilidade passiva não consome uma ação de uso.");
                RuleState.Require(ability.MaximumUses==0 || ability.RemainingUses>0,"Sem usos disponíveis.");Spend(ability.Cost);
                if(ability.MaximumUses>0)abilities[abilityIndex]=ability with{RemainingUses=ability.RemainingUses-1};
                var value=ability.Roll.Length==0?null:(int?)DamageFormula.Evaluate(ability.Roll,s);
                return Result($"{ability.Name}: uso e custo registrados{(value is null?"":$"; resultado {value}")}. Consulte o efeito e selecione os alvos; descrições não executam regras automaticamente.");
            case "ability-reset":
                var resetIndex=Array.FindIndex(abilities,a=>a.Id==command.Option);RuleState.Require(resetIndex>=0,"Habilidade não encontrada.");abilities[resetIndex]=abilities[resetIndex] with{RemainingUses=abilities[resetIndex].MaximumUses};return Result("Usos restaurados por confirmação do mestre.");
            case "condition":
            case "condition-persistent":
            case "condition-remove":
                RuleState.Require(D20Conditions.Choices(true).Any(c=>c.Value==command.Option),"Condição inválida.");
                if(command.Action!="condition-remove" && ParanormalThreatSchema.ImmuneCondition(sheet,command.Option!)) return Result("Condição impedida por imunidade; ficha preservada.");
                message=D20Conditions.Apply(s,new(command.Action,command.Amount,command.Option));return Result(message);
            case "round": message=D20Conditions.Apply(s,new("condition-round"));return Result(message);
            case "scene":
                D20Conditions.EndScene(s);sheet=sheet with{PresenceTargets=[]};abilities=abilities.Select(a=>a.Recharge=="scene"?a with{RemainingUses=a.MaximumUses}:a).ToArray();return Result("Nova cena confirmada; durações de cena e registro de presença encerrados; habilidades configuradas recuperadas.");
            case "turn":
                message=D20Conditions.StartTurn(s);abilities=abilities.Select(a=>a.Recharge=="turn"?a with{RemainingUses=a.MaximumUses}:a).ToArray();
                var recover=s.Health>0?Math.Max(0,Math.Min(sheet.FastHealing.Amount,maximum-s.Health-s.Get("cura_impedida"))):0;s.Health+=recover;
                if(recover>0)message+=$"Cura Acelerada recuperou {recover} PV. ";
                if(D20Conditions.Has(s,"em_chamas"))
                {
                    var fire=ResolveDamage(sheet,s,new("damage","fogo",s.Die(6)));var oldFireHealth=s.Health;s.Health-=fire.Applied;
                    if(sheet.FastHealing.Except?.Contains("fogo")==true)s.Set("cura_impedida",Math.Min(maximum,s.Get("cura_impedida")+oldFireHealth-s.Health));
                    message+=$"Chamas: {fire.Raw} → {fire.Applied} dano. ";
                }
                if(s.Get("sangrando")>0)
                {
                    var bleeding=s.Roll(D20Checks.ParanormalExpression(s,"vigor",0));if(D20Checks.Passed(s,bleeding,20,true))s.Set("sangrando",0);else s.Health-=s.Die(6);
                    message+=$"Sangramento: Vigor {bleeding}/DT 20. ";
                }
                if(D20Conditions.Has(s,"asfixiado"))
                {
                    s.Set("asfixia_rodadas",s.Get("asfixia_rodadas")+1);
                    if(s.Get("asfixia_rodadas")>sheet.Attributes.Vigor && s.Get("asfixia_inconsciente")==0)
                    {
                        var difficulty=5+5*s.Get("asfixia_testes");var fort=sheet.Skills.SingleOrDefault(k=>k.Id=="fortitude")?.Bonus??0;
                        var breath=s.Roll(D20Checks.ParanormalExpression(s,"vigor",fort,key:"per_fortitude"));s.Set("asfixia_testes",s.Get("asfixia_testes")+1);
                        if(!D20Checks.Passed(s,breath,difficulty,true))s.Set("asfixia_inconsciente",1);message+=$"Asfixia: Fortitude {breath}/DT {difficulty}. ";
                    }
                    if(s.Get("asfixia_inconsciente")>0){var oldBreathHealth=s.Health;s.Health-=s.Die(6);s.Set("cura_impedida",Math.Min(maximum,s.Get("cura_impedida")+oldBreathHealth-s.Health));}
                }
                return Result(message+"Turno da ameaça iniciado; durações de rodada exigem concluir a rodada.");
            case "context":
                var context=command.Option!;var max=context=="dados_zero_penalidade"?40:context is "camuflagem_alvo" or "sentido_percepcao"?2:1;
                RuleState.Require((context is "dados_zero_penalidade" or "arma_leve" or "camuflagem_alvo" or "ignora_camuflagem" or "sentido_percepcao") && command.Amount<=max,"Contexto inválido.");s.Set(context,command.Amount);return Result("Contexto do próximo teste atualizado.");
            case "enigma": return Result("Enigma atualizado pelo mestre.") with{Sheet=sheet with{EnigmaSolved=command.Amount==1}};
            default:throw new ArgumentException("Ação de ameaça não suportada.");
        }
    }
    public static ThreatDamageResult ResolveDamage(ParanormalThreatSheet sheet,RuleState s,ThreatCommand command)
    {
        RuleState.Require(ParanormalThreatSchema.DamageTypes.Contains(command.Option!),"Escolha um tipo de dano de Ordem 1.3.");
        RuleState.Require(command.Source is "normal" or "fear-ritual" or "hunger" or "thirst" or "suffocation","Fonte de dano inválida.");
        var amount=command.Amount;
        var sourceLoss=command.Source is "hunger" or "thirst" or "suffocation";
        if(!sourceLoss && command.Critical && sheet.CriticalImmune){RuleState.Require(command.NormalAmount is >=0 and <=9999,"Alvo imune a críticos: informe o dano normal, sem os dados adicionais.");amount=command.NormalAmount!.Value;}
        var defenses=sourceLoss?[]:sheet.Defenses.Where(d=>(!d.DisabledByEnigma||!sheet.EnigmaSolved) && (d.Type=="all"||d.Type==command.Option||d.Type=="paranormal"&&ParanormalThreatSchema.Elements.Contains(command.Option!)) && d.Except?.Contains(command.Option!)!=true).ToArray();
        var immune=!sourceLoss && (defenses.Any(d=>d.Effect=="immunity") || sheet.Kind=="paranormal"&&(command.Option=="mental"||command.Source=="fear-ritual"));
        string Group(ThreatDefense d)=>d.SourceKind=="ability"?$"ability:{d.SourceId}":d.SourceKind;
        var multiplier=1+defenses.Where(d=>d.Effect=="vulnerability").Select(Group).Distinct().Count();
        var reduction=command.IgnoreResistance||sourceLoss?0:defenses.Where(d=>d.Effect=="reduction").GroupBy(Group).Sum(g=>g.Max(d=>d.Amount))+(D20Conditions.Has(s,"petrificado")?10:0);
        var afterSave=!sourceLoss&&command.Half?amount/2:amount;
        var applied=immune?0:Math.Max(0,afterSave*multiplier-reduction);
        string[] sources=[..defenses.Select(d=>d.Source.Length>0?d.Source:d.Effect),..(sheet.Kind=="paranormal"&&(command.Option=="mental"||command.Source=="fear-ritual")?new[]{"Origem Paranormal"}:[])];
        return new(amount,afterSave,multiplier,reduction,applied,immune,sources.Distinct().ToArray());
    }
}
