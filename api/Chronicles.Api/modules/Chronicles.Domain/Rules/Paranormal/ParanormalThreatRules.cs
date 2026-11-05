namespace Chronicles.Domain;

public sealed record ThreatCommand(string Action, string? Option = null, int Amount = 0, string Visibility = "gm",
    bool Half = false, bool IgnoreResistance = false, string Source = "normal", bool Critical = false, int? NormalAmount = null,
    Guid? TargetId = null, int? TargetVersion = null, int AdditionalCreatures = 0, int SneakDamage = 0, ThreatSensoryContext? PerceptionContext = null);
public sealed record ThreatAttackOutcome(int Index, int Total, int Natural, bool? Hit, bool Critical, int Damage, string DamageType, int ExtraDamage, string ExtraType, int MissChance = 0, int? ConcealmentRoll = null);
public sealed record ThreatDamageResult(int Raw, int AfterSave, int Multiplier, int Reduction, int Applied, bool Immune, string[] Sources, bool CriticalPrevented = false, int? FortificationRoll = null, bool SneakPrevented = false);
public sealed record ThreatResult(ParanormalThreatSheet Sheet,int Health,string Message,RuleRoll[] Rolls,ThreatAttackOutcome[]? Attacks=null,ThreatDamageResult? Damage=null,ThreatPerceptionView? Perception=null);

public static class ParanormalThreatRules
{
    public static ThreatResult Apply(ParanormalThreatSheet sheet,int health,int maximum,ThreatCommand command,Func<int,int> draw)
    {
        if(ParanormalThreatSchema.Validate(sheet) is {} error) throw new ArgumentException(error);
        RuleState.Require(command.Amount is >=0 and <=9999 && command.Visibility is "gm" or "private" or "campaign", "Valor ou privacidade inválidos.");
        var s=ParanormalThreatSchema.ToState(sheet,health,maximum,draw);
        var perception=command.Action is "attack" or "check"?ParanormalThreatPerception.Prepare(sheet,s):ParanormalThreatPerception.Resolve(sheet,s);
        var attacks=sheet.Attacks.ToArray();var abilities=sheet.Abilities.ToArray();
        string message;
        void Spend(int cost) { if(cost>0) s.SpendMana(cost+D20Conditions.CostIncrease(s)); }
        ThreatResult Result(string text,ThreatAttackOutcome[]? outcomes=null,ThreatDamageResult? damage=null) => new(sheet with{Attacks=attacks,Abilities=abilities,Effort=s.Mana,State=new(s.State)},s.Health,text,s.Rolls.ToArray(),outcomes,damage,perception);
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
                    var attackAttribute=sheet.Incorporeal?"agilidade":attack.Attribute;
                    var severity=D20Conditions.Severity(s,attackAttribute,attack.Style=="ranged"?"per_pontaria":"per_luta",true,attack.Style=="ranged",true);
                    var expression=D20Checks.ParanormalExpression(s,attackAttribute,attack.Bonus,severity);
                    var total=s.Roll(expression);var roll=s.Rolls[^1];var natural=roll.Rolls[roll.KeptIndexes[0]];
                    var hit=natural==20 || command.Amount==0 || total>=command.Amount;
                    int? camouflageRoll=null;
                    if(hit && perception.MissChance>0){camouflageRoll=s.Die(10);if(camouflageRoll<=perception.MissChance/10)hit=false;}
                    var critical=hit && natural>=attack.Margin;
                    var damage=hit?Math.Max(0,DamageFormula.Evaluate(attack.Damage,s,critical?attack.Multiplier:1)):0;
                    var extra=hit?Math.Max(0,DamageFormula.Evaluate(attack.ExtraDamage,s)):0;
                    outcomes.Add(new(i+1,total,natural,command.Amount==0&&hit?null:hit,critical,damage,attack.DamageType,extra,attack.ExtraType,perception.MissChance,camouflageRoll));
                }
                if(attack.Ammunition>=0) attacks[index]=attack with{Ammunition=attack.Ammunition-attack.Count};
                return Result($"{attack.Name}: {attack.Count} ataque(s) resolvido(s), custo e munição registrados. {(command.Amount==0?"Confirme os acertos; Defesa não informada.":"Aplique cada acerto ao alvo separadamente, após as defesas dele.")}",outcomes.ToArray());
            case "check":
                var attribute=command.Option is not null && ParanormalThreatSchema.Attributes.Contains(command.Option)?command.Option:null;
                var skill=sheet.Skills.SingleOrDefault(x=>x.Id==command.Option);
                RuleState.Require(attribute is not null || skill is not null,"Teste não encontrado.");
                RuleState.Require(!sheet.Incorporeal || (attribute??skill!.Attribute)!="forca","Ameaças incorpóreas não têm Força e não fazem testes desse atributo.");
                var key=attribute??"per_"+skill!.Id;
                if(skill?.Id=="reflexos" && D20Conditions.Has(s,"indefeso")) return Result("Reflexos: falha automática por indefeso.");
                if(skill?.Id=="percepcao" && (s.Get("sentido_percepcao")==1 && (D20Conditions.Has(s,"cego")||sheet.SensoryContext.Active&&!perception.VisualCheckAvailable) || s.Get("sentido_percepcao")==2 && D20Conditions.Has(s,"surdo"))) return Result("Percepção: sentido exigido indisponível neste contexto. Sentidos alternativos não identificam cores, escrita ou sons automaticamente.");
                var check=D20Checks.ParanormalExpression(s,attribute??skill!.Attribute,skill?.Bonus??0,key:key);
                var checkedTotal=s.Roll(check);message=$"{skill?.Name??attribute}: {check} = {checkedTotal}";
                if(command.Amount>0) message+=$" / DT {command.Amount}: {(D20Checks.Passed(s,checkedTotal,command.Amount,true)?"sucesso":"falha")}";
                return Result(message+".");
            case "damage":
                var resolved=ResolveDamage(sheet,s,command);
                var before=s.Health;s.Health-=resolved.Applied;
                if(s.Health<before && (sheet.FastHealing.Except?.Contains(command.Option!)==true || command.Source is "hunger" or "thirst" or "suffocation")) s.Set("cura_impedida",Math.Min(maximum,s.Get("cura_impedida")+before-s.Health));
                return Result($"Dano {resolved.Raw} → {resolved.Applied}; {(resolved.Immune?"imunidade aplicada":$"multiplicador ×{resolved.Multiplier}, RD {resolved.Reduction}")}. {(resolved.FortificationRoll is {} fortification?$"Fortificação: d100 = {fortification}. ":"")}{(resolved.CriticalPrevented?"Crítico impedido; usado o dano normal. ":"")}{(resolved.SneakPrevented?"Dano furtivo impedido. ":"")}{s.Health}/{maximum} PV.",damage:resolved);
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
                if(command.Action!="condition-remove" && ParanormalThreatDefenses.ConditionBlocked(sheet,command) is {} blocked)return Result(blocked);
                if(command.Action!="condition-remove" && ParanormalThreatSchema.ImmuneCondition(sheet,command.Option!)) return Result("Condição impedida por imunidade; ficha preservada.");
                message=D20Conditions.Apply(s,new(command.Action,command.Amount,command.Option));
                if(command.Option=="em_chamas")s.Set("chamas_origem",command.Action=="condition-remove"?0:Array.IndexOf(ParanormalThreatDefenses.EffectSources,command.Source)+1);
                return Result(message);
            case "round": message=D20Conditions.Apply(s,new("condition-round"));return Result(message);
            case "scene":
                D20Conditions.EndScene(s);if(sheet.SensoryContext.Active){s.Set("camuflagem_alvo",0);s.Set("ignora_camuflagem",0);}sheet=sheet with{PresenceTargets=[],SensoryContext=new()};perception=ParanormalThreatPerception.Resolve(sheet,s);abilities=abilities.Select(a=>a.Recharge=="scene"?a with{RemainingUses=a.MaximumUses}:a).ToArray();return Result("Nova cena confirmada; durações de cena, contexto do oponente e registro de presença encerrados; habilidades configuradas recuperadas.");
            case "turn":
                message=D20Conditions.StartTurn(s);abilities=abilities.Select(a=>a.Recharge=="turn"?a with{RemainingUses=a.MaximumUses}:a).ToArray();
                var recover=s.Health>0?Math.Max(0,Math.Min(sheet.FastHealing.Amount,maximum-s.Health-s.Get("cura_impedida"))):0;s.Health+=recover;
                if(recover>0)message+=$"Cura Acelerada recuperou {recover} PV. ";
                if(D20Conditions.Has(s,"em_chamas"))
                {
                    var fireOrigin=s.Get("chamas_origem");var fireSource=fireOrigin is >=1 and <=5?ParanormalThreatDefenses.EffectSources[fireOrigin-1]:"normal";
                    var fire=ResolveDamage(sheet,s,new("damage","fogo",s.Die(6),Source:fireSource));var oldFireHealth=s.Health;s.Health-=fire.Applied;
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
            case "perception-context":
                RuleState.Require(ParanormalThreatPerception.ValidateContext(command.PerceptionContext) is null,"Informe um contexto sensorial válido.");
                sheet=sheet with{SensoryContext=command.PerceptionContext!};s.Set("camuflagem_alvo",0);
                perception=ParanormalThreatPerception.Resolve(sheet,s);return Result("Contexto do oponente salvo; sentidos aplicados somente conforme a distância e a origem informadas.");
            case "enigma": return Result("Enigma atualizado pelo mestre.") with{Sheet=sheet with{EnigmaSolved=command.Amount==1}};
            default:throw new ArgumentException("Ação de ameaça não suportada.");
        }
    }
    public static ThreatDamageResult ResolveDamage(ParanormalThreatSheet sheet,RuleState s,ThreatCommand command)
    {
        RuleState.Require(ParanormalThreatSchema.DamageTypes.Contains(command.Option!),"Escolha um tipo de dano de Ordem 1.3.");
        RuleState.Require(command.SneakDamage>=0 && command.SneakDamage<=command.Amount,"Dano furtivo deve estar incluído no total informado, entre zero e esse total.");
        RuleState.Require(ParanormalThreatDefenses.EffectSources.Contains(command.Source) || command.Source is "hunger" or "thirst" or "suffocation","Fonte de dano inválida.");
        var sourceLoss=command.Source is "hunger" or "thirst" or "suffocation";
        var defenses=sourceLoss?[]:sheet.Defenses.Where(d=>(!d.DisabledByEnigma||!sheet.EnigmaSolved) && (d.Type=="all"||d.Type==command.Option||d.Type=="paranormal"&&ParanormalThreatSchema.Elements.Contains(command.Option!)) && d.Except?.Contains(command.Option!)!=true).ToArray();
        var incorporealBlocked=!sourceLoss && !ParanormalThreatDefenses.CanAffect(sheet,command.Source);
        var immune=!sourceLoss && (incorporealBlocked || defenses.Any(d=>d.Effect=="immunity") || sheet.Kind=="paranormal"&&(command.Option=="mental"||command.Source=="fear-ritual"));
        bool criticalPrevented=false,sneakPrevented=false;int? fortificationRoll=null;
        var amount=sourceLoss||immune?command.Amount:ParanormalThreatDefenses.ProtectedDamage(sheet,s,command,out criticalPrevented,out sneakPrevented,out fortificationRoll);
        string Group(ThreatDefense d)=>d.SourceKind=="ability"?$"ability:{d.SourceId}":d.SourceKind;
        var multiplier=1+defenses.Where(d=>d.Effect=="vulnerability").Select(Group).Distinct().Count();
        var reduction=command.IgnoreResistance||sourceLoss?0:defenses.Where(d=>d.Effect=="reduction").GroupBy(Group).Sum(g=>g.Max(d=>d.Amount))+(D20Conditions.Has(s,"petrificado")?10:0);
        var afterSave=!sourceLoss&&command.Half?amount/2:amount;
        var applied=immune?0:Math.Max(0,afterSave*multiplier-reduction);
        string[] sources=[..defenses.Select(d=>d.Source.Length>0?d.Source:d.Effect),..(sheet.Kind=="paranormal"&&(command.Option=="mental"||command.Source=="fear-ritual")?new[]{"Origem Paranormal"}:[]),..incorporealBlocked?new[]{"Incorpóreo"}:[],..criticalPrevented&&sheet.CriticalImmune?new[]{"Imunidade a críticos"}:[],..sneakPrevented||criticalPrevented&&!sheet.CriticalImmune?new[]{"Fortificação"}:[]];
        return new(amount,afterSave,multiplier,reduction,applied,immune,sources.Distinct().ToArray(),criticalPrevented,fortificationRoll,sneakPrevented);
    }
}
