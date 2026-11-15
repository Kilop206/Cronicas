using System.Globalization;
using System.Text.Json;

namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static string NewDay(RuleState s) { RuleState.Require(s.Get("combate")==0,"Encerre o combate antes de confirmar um novo dia."); s.Set("antinatural_dia",0); return "Novo dia confirmado: reservas diárias liberadas."; }
    private static string EndCombat(RuleState s)
    {
        s.Set("combate",0); s.Set("turno_aberto",0); s.Set("sequencia",0);
        foreach(var key in new[]{"autossuficiente_reforco","alvo_derrotado_pendente","renovacao_critico","especial_preciso","fund_duplicado","fund_rapido","adepto_feiticaria_usos"})s.Set(key,0);
        foreach(var key in new[]{"kokusen_bonus","kokusen_reducao","kokusen_duracao","cobertura_temp","empolgacao","empolgacao_acerto","defesa_empolgacao","defesa_descendente","corpo_treinado_pronto","ultimo_corpo_a_corpo"})s.Set(key,0);
        foreach(var key in s.State.Keys.Where(k=>k.StartsWith("acao_",StringComparison.Ordinal)).ToArray()) s.Set(key,0);
        return "Cena de combate encerrada. Recursos temporários e canalização são preservados; encerre os temporários quando a fonte terminar.";
    }
    private static string Check(RuleState s,RuleCommand c)
    {
        var option=c.Option!; var save=option.StartsWith("res_",StringComparison.Ordinal); var key=option.Length>4?option[4..]:option;
        if(option=="iniciativa")
        {
            var bonus=s.N("iniciativa",FmCatalog.Mod(s,"destreza"))-s.Get("exaustao")-FmConditions.SoulPenalty(s)-(FmConditions.Active(s).Contains("surdo")?5:0);
            FmConditions.Roll(s,bonus,FmConditions.Active(s).Contains("invisivel")); return $"Iniciativa: {s.Rolls[^1].Total}.";
        }
        var check=(save?FmCatalog.Saves:FmCatalog.Skills).Single(x=>x.Id==key);
        RuleState.Require(!check.TrainedOnly || FmConditions.Training(s,key)>0,"Essa perícia exige treinamento.");
        if(save)
        {
            var degree=FmConditions.Save(s,key,c.Amount); return $"{check.Name}: {new[]{"falha","sucesso (metade do dano)","sucesso crítico (ignora dano e condições)"}[degree]}.";
        }
        FmConditions.Roll(s,FmConditions.Bonus(s,check)); return $"{check.Name}: {s.Rolls[^1].Total}"+(c.Amount>0?(s.Rolls[^1].Total>=c.Amount?" · sucesso.":" · falha."):".");
    }
    private static string Condition(RuleState s,RuleCommand c,bool remove)
    {
        RuleState.Require(c.Amount<=100,"Use no máximo 100 rodadas."); s.Set("cond_"+c.Option,remove?0:c.Amount==0?-1:c.Amount);
        if(FmConditions.Incapacitated(s)) { s.Set("concentrando",0); StopSustainedConcentration(s); if(s.Get("dominio")>0) EndDomain(s); }
        return remove?"Condição removida. Condições derivadas de outra fonte permanecem ativas.":"Condição aplicada. Penalidades iguais usam a mais severa; confira efeitos contextuais na referência.";
    }
    private static void UseAction(RuleState s,string action)
    {
        RuleState.Require(!FmConditions.Incapacitated(s),"A condição atual impede ações físicas; ações puramente mentais exigem adjudicação do narrador.");
        if(s.Get("combate")==0) return;
        RuleState.Require(action=="reacao" || s.Get("turno_aberto")==1,"Ações comuns, bônus e de movimento exigem seu turno aberto.");
        RuleState.Require(action is "comum" or "bonus" or "movimento" or "reacao" or "completa", "Tipo de ação inválido.");
        if(action=="completa") { UseAction(s,"comum"); UseAction(s,"bonus"); return; }
        RuleState.Require(s.Get("acao_"+action)>0,"Essa ação já foi usada no turno.");
        if(action=="reacao") RuleState.Require(!FmConditions.Active(s).Overlaps(["desorientado","surpreso"]),"Desorientado/Surpreso: confira a fonte e remova a condição quando o gatilho terminar antes de usar esta reação.");
        s.Set("acao_"+action,s.Get("acao_"+action)-1); s.Set("descanso_curto",0);
    }
    private static string Action(RuleState s,string action)
    {
        RuleState.Require(s.Get("combate")==1,"Inicie seu turno para controlar ações.");
        if(action.Contains('_'))
        {
            RuleState.Require(!FmConditions.Active(s).Contains("enjoado"),"Enjoado impede conversão de ações.");
            var parts=action.Split('_'); UseAction(s,parts[0]); s.Set("acao_"+parts[1],s.Get("acao_"+parts[1])+1);
        }
        else if(action=="levantar") { RuleState.Require(!FmConditions.Active(s).Contains("imovel"),"Imóvel impede Levantar."); UseAction(s,"movimento"); s.Set("cond_caido",0); }
        else UseAction(s,action);
        return "Ação registrada.";
    }
    private static string StartTurn(RuleState s,FmActionContext? context=null)
    {
        RuleState.Require(s.Get("turno_aberto")==0,"Conclua o turno atual antes de iniciar outro.");
        StartMartialTurn(s,s.Get("combate")==0);
        s.Set("combate",1); s.Set("turno_aberto",1); s.Set("sequencia",0); s.Set("ritual_usado",0);s.Set("cobertura_temp",0);
        foreach(var action in new[]{"comum","bonus","movimento","reacao"}) s.Set("acao_"+action,1);
        s.Set("descanso_curto",0);
        var notes=new List<string>{AntiDomainTurn(s,context??new())};
        if(s.Get("cond_sangramento")!=0)
        {
            var expression=new[]{"2d6","3d8","4d10","6d10"}[Math.Clamp(s.Get("sangramento_nivel",1),1,4)-1]; var loss=s.Roll(expression);
            ApplyHealthLoss(s,loss,false); notes.Add($"Sangramento: {loss} de perda de vida.");
        }
        foreach(var row in FmCatalog.Rows(s,"feiticos").Select((value,index)=>(value,index)))
        {
            if(s.Get("sustento_"+row.index)==0) continue;
            var cost=FmCatalog.Number(row.value,"nivel")<=2?1:2;
            if(s.Mana+s.Get("energia_temp")>=cost+FmConditions.CostPenalty(s)) Spend(s,cost); else { s.Set("sustento_"+row.index,0); notes.Add("Um feitiço sustentado foi encerrado por falta de recurso."); }
        }
        if(s.Get("morrendo")>0 && s.Get("morto")==0) notes.Add(DeathSave(s));
        if(s.Get("ritual_spell")>0 && s.Get("morto")==0)
        {
            s.Set("ritual_pronto",1);
            if(s.Get("ritual_mode")==1) notes.Add(FinishRitual(s,new("fm-ritual-finish")));
        }
        return "Turno iniciado. "+string.Join(" ",notes);
    }
    private static string EndTurn(RuleState s)
    {
        RuleState.Require(s.Get("turno_aberto")==1,"Nenhum turno aberto."); s.Set("turno_aberto",0);
        if(s.Get("cond_sangramento")!=0 && s.Get("morto")==0)
        {
            var degree=FmConditions.Save(s,"fortitude",s.Get("sangramento_cd")); if(degree>=(s.Get("sangramento_nivel",1)==4?2:1)) s.Set("cond_sangramento",0);
        }
        foreach(var c in FmCatalog.Conditions.Where(c=>c!="sangramento")) if(s.Get("cond_"+c)>0) s.Set("cond_"+c,s.Get("cond_"+c)-1);
        if(s.Get("dominio")>0) { s.Set("dominio_rodadas",Math.Max(0,s.Get("dominio_rodadas")-1)); if(s.Get("dominio_rodadas")==0) EndDomain(s); }
        else if(s.Get("exaustao_tecnica")>0) s.Set("exaustao_tecnica",s.Get("exaustao_tecnica")-1);
        if(s.Get("maxima_usada_turno")>0)s.Set("maxima_usada_turno",0);else if(s.Get("maxima_recarga")>0)s.Set("maxima_recarga",s.Get("maxima_recarga")-1);
        if(s.Get("kokusen_duracao")>0){s.Set("kokusen_duracao",s.Get("kokusen_duracao")-1);if(s.Get("kokusen_duracao")==0)s.Set("kokusen_reducao",0);}
        return "Turno concluído; durações reduzidas. Resolva os testes de resistência de condições da fonte com a CD original.";
    }
    private static string Sequence(RuleState s,int amount) { RuleState.Require(amount>0,"Informe a quantidade de ataques."); s.Set("sequencia",Math.Min(10,s.Get("sequencia")+amount)); return "Sequência registrada: −1 Defesa por dois ataques, máximo −5; reinicia no seu turno."; }
    private static string Exhaustion(RuleState s,int amount)
    {
        RuleState.Require(amount<=6,"Exaustão deve estar entre 0 e 6."); s.Set("exaustao",amount); s.ChangeHealthMaximum(EffectiveHealthMaximum(s));
        if(amount==6) s.Set("morto",1); return amount==6?"Exaustão 6: morte.":s.Get("desmaiado")>0?"Exaustão: desmaiado. Confira a duração dos descansos com o narrador.":"Exaustão atualizada; penalidades cumulativas aplicadas.";
    }
    private static string Concentration(RuleState s,RuleCommand c)
    {
        if(c.Option=="iniciar") { RuleState.Require(!FmConditions.Incapacitated(s),"Incapaz de se concentrar."); StopSustainedConcentration(s); s.Set("concentrando",1); return "Concentração iniciada; apenas uma habilidade concentrada por vez."; }
        if(c.Option=="encerrar") { s.Set("concentrando",0); s.Set("simples_durabilidade",0);s.Set("simples_raio_meios",0);StopSustainedConcentration(s); return "Concentração encerrada."; }
        RuleState.Require(s.Get("concentrando")>0,"Nenhuma concentração ativa.");
        if(FmConditions.Save(s,"fortitude",c.Amount,true)>0) return "Concentração mantida.";
        LoseConcentration(s);return s.Get("concentrando")>0?"Falha de concentração: anti-domínio perdeu durabilidade.":"Concentração perdida; resolva o acerto garantido imediatamente se o anti-domínio quebrou.";
    }
    private static void StopSustainedConcentration(RuleState s) { foreach(var key in s.State.Keys.Where(k=>k.StartsWith("concentrado_",StringComparison.Ordinal)).ToArray()) s.Set(key,0);s.Set("simples_durabilidade",0);s.Set("simples_raio_meios",0);s.Set("cesta_durabilidade",0);s.Set("cesta_selo",0);if(s.Get("ritual_mode")==2)CancelRitual(s); }
    private static string Attack(RuleState s,RuleCommand c)
    {
        var rows=FmCatalog.Rows(s,"armas"); var index=int.Parse(c.Option!,CultureInfo.InvariantCulture); var row=rows[index];
        var context=c.Jujutsu??new();var kind=context.AttackKind;var melee=kind is "melee" or "martial" or "unarmed" or "offhand";
        RuleState.Require(s.Get("cesta_selo")==0||kind=="unarmed","O selo da Cesta ocupa as duas mãos; encerre a concentração ou libere o selo no início do turno antes de usar armas.");
        RuleState.Require(kind is "unspecified" or "melee" or "ranged" or "thrown" or "martial" or "unarmed" or "offhand" && context.Concealment is 0 or 20 or 50,"Contexto de ataque inválido.");
        RuleState.Require(kind!="offhand"||context.OffhandLight,"O ataque com a segunda arma exige confirmar que ambas as armas são leves.");
        RuleState.Require(FmCatalog.Number(row,"municao",-1)!=0,"Arma sem munição carregada; recarregue antes do ataque.");
        RuleState.Require(context.FighterManeuver is "none" or "ajuste" or "desarme" or "comando","Manobra de ataque inválida.");
        var artDice=BeginCombatArt(s,context,melee);
        var style=StyleAttack(s,context);var special=BeginSpecialStrike(s,context);
        if(context.CombatArt!="arremesso_agil")MartialAttackAction(s,context,FmCatalog.Text(row,"acao"),special.Contains("lento"));
        else { RuleState.Require(!FmConditions.Incapacitated(s),"Incapaz de atacar.");if(special.Contains("lento"))UseAction(s,"completa"); }
        if(context.FighterManeuver!="none")UseManeuver(s,context.FighterManeuver);
        if(context.FighterManeuver=="comando")Spend(s,1);
        var active=FmConditions.Active(s); var bonus=FmCatalog.Mod(s,FmCatalog.Text(row,"atributo"))+FmCatalog.Level(s)/2+(FmCatalog.Flag(row,"treinado")?FmCatalog.Training(s):0)-s.Get("exaustao")-FmConditions.SoulPenalty(s);
        bonus-=new[]{active.Contains("envenenado")?2:0,active.Contains("amedrontado")?3:active.Contains("abalado")?1:0,active.Contains("enredado")?2:0,active.Contains("caido")&&melee?3:0}.Max();
        var fighter=FmCatalog.ClassLevel(s,"lutador");var fighterEffect=FmCatalog.EffectLevel(s,"lutador");var control=Aptitude(s,"controle");
        if(fighter>=5&&kind is "unarmed" or "martial")bonus+=2+(fighterEffect>=8?1:0)+(fighterEffect>=12?1:0)+(fighterEffect>=16?1:0)+(fighterEffect>=20?1:0);
        if(FmCatalog.ClassLevel(s,"restringido")==20)bonus+=5;
        if(s.Get("kokusen_bonus")>0)bonus+=control/2;
        if(context.TargetExposed)bonus+=4;
        if(context.FighterManeuver=="ajuste")bonus+=RollExcitement(s);
        bonus+=style.Attack-4*special.Count(v=>v=="desfocado");
        var natural=FmConditions.Roll(s,bonus,context.Advantage||special.Contains("preciso"),context.Disadvantage); var total=s.Rolls[^1].Total; var hit=context.TargetUnconscious || natural!=1 && (natural==20 || c.Amount==0 || total>=c.Amount);
        var concealment=active.Contains("cego")?50:context.Concealment;
        if(hit&&concealment>0&&s.Die(100)<=concealment)hit=false;
        var critical=hit && (context.TargetUnconscious||context.TargetParalyzed&&melee||natural>=FmCatalog.Number(row,"margem",20)-(special.Contains("letal")?1:0)); var damage=0;var black=false;
        if(hit)
        {
            var formula=kind=="unarmed"?UnarmedDamage(s):FmCatalog.Text(row,"dados");
            var damageStart=s.Rolls.Count;damage=RollDamage(s,formula,critical);
            var extraWeaponDice=(special.Contains("atroz")?1:0)+(FmCatalog.ClassLevel(s,"combate")==20?1:0);
            if(extraWeaponDice>0){RuleState.Require(DiceExpression.TryParse(formula,out var weaponDice)&&weaponDice is not null,"Um dado da arma é necessário para Atroz/Autossuficiente.");damage+=s.Pool(extraWeaponDice*(critical?2:1),weaponDice!.Sides);}
            if(style.Massive)damage+=RerollDamageDice(s,damageStart,s.Rolls.Count,80,false,true);
            damage+=style.Damage+(kind=="offhand"&&!FmClassChoices.Style(s,"duplo")?0:FmCatalog.Mod(s,FmCatalog.Text(row,"atributo")));
            if(fighter>=5)damage+=1+(fighterEffect>=9?1:0)+(fighterEffect>=13?1:0)+(fighterEffect>=17?1:0);
            if(FmCatalog.ClassLevel(s,"restringido")==20)damage+=FmCatalog.Level(s)/2;
            if(s.Get("kokusen_bonus")>0)damage+=control;
            if(context.TargetExposed)damage+=FmCatalog.Level(s);
            if(context.FighterManeuver is "ajuste" or "desarme")damage+=RollExcitement(s,critical);
            if(artDice>0)damage+=s.Pool(artDice*(critical?2:1),6);
            if(context.SneakAttack)
            {
                var restricted=FmCatalog.ClassLevel(s,"restringido");
                RuleState.Require(restricted>=2&&s.Get("ataque_furtivo_usado")==0&&(context.TargetOffguard||context.TargetFlanked),"Ataque Furtivo exige Restringido 2, alvo Desprevenido ou flanqueado e um uso por turno.");
                damage+=s.Pool((restricted>=3?Math.Min(6,1+restricted/3):1)*(critical?2:1),8);s.Set("ataque_furtivo_usado",1);
            }
            if(s.Get("canalizacao_pe")>0)
            {damage+=s.Pool(s.Get("canalizacao_pe")*(critical?2:1),s.Get("canalizacao_dado"));if(FmCatalog.Has(s,"canalizacao_maxima"))damage+=Aptitude(s,"aura");s.Set("canalizacao_pe",0);s.Set("canalizacao_dado",0);}
            damage-=FmConditions.OutputPenalty(s);
            black=BlackFlash(s,natural,melee);
            if(black)damage=damage*3/2;
            var after=FmCatalog.Text(row,"apos"); if(after.Length>0) damage+=s.Roll(after);
            damage=Math.Max(0,damage);
        }
        CompleteCombatStrike(s,hit,critical,context.TargetEnemy);
        s.Set("ultimo_corpo_a_corpo",melee?1:0);
        if(s.Get("turno_aberto")==1)
        {
            if(hit&&fighter>0)s.Set("empolgacao_acerto",1);
            if(fighter>0&&kind is "unarmed" or "martial")s.Set("corpo_treinado_pronto",1);
            if(hit&&context.CombatArt=="golpe_descendente")s.Set("defesa_descendente",Math.Max(0,FmCatalog.Mod(s,"sabedoria")/2));
        }
        var ammunition=FmCatalog.Number(row,"municao",-1);
        if(ammunition>0)
        {
            var mutable=rows.Select(r=>r.Deserialize<Dictionary<string,JsonElement>>()!).ToArray(); mutable[index]["municao"]=JsonSerializer.SerializeToElement(ammunition-1); FmCatalog.Rows(s,"armas",mutable);
        }
        var note=context.CombatArt=="investida_imediata"?$" Avance até {Math.Max(0,FmCatalog.Mod(s,"sabedoria"))*1.5m} m sem provocar oportunidade.":"";
        if(hit&&context.CombatArt=="distracao_letal")note+=$" Alvo: −{Math.Max(0,FmCatalog.Mod(s,"sabedoria")/2)} Defesa por uma rodada; aplique na ficha do alvo.";
        if(hit&&context.FighterManeuver=="desarme")note+=$" Alvo deve superar {total} com ataque corpo a corpo ou largar o item escolhido.";
        if(context.FighterManeuver=="comando")note+=" Aliado a até 1,5 m pode atacar corpo a corpo o mesmo alvo usando a própria reação; você pagou 1 PE. Não use Ataque Extra com Comando.";
        return $"{FmCatalog.Text(row,"nome")}: {total} · "+(hit?$"{(critical?"crítico · ":"")}{(black?"Raio Negro · ignora RD e resistência · ":"")}{Math.Max(0,damage)} de dano para aplicar no alvo.":natural==1?"desastre; o adversário pode reagir.":"errou.")+note+SpecialStrikeDescription(s,special,hit,style.Massive);
    }
    private static string UnarmedDamage(RuleState s)
    {
        var level=FmCatalog.Level(s);var enhanced=FmCatalog.ClassLevel(s,"lutador")>0||FmCatalog.ClassLevel(s,"restringido")>0;
        if(enhanced){var classLevel=Math.Max(FmCatalog.EffectLevel(s,"lutador"),FmCatalog.EffectLevel(s,"restringido"));return classLevel>=17?FmCatalog.ClassLevel(s,"lutador")==20?"3d12":"2d12":classLevel>=13?"2d8":classLevel>=9?"1d12":classLevel>=5?"1d10":"1d8";}
        return level>=17?"1d12":level>=13?"1d10":level>=9?"1d8":level>=5?"1d6":"1d4";
    }
    private static bool BlackFlash(RuleState s,int natural,bool melee)
    {
        if(!melee||!FmCatalog.Has(s,"raio_negro"))return false;
        RuleState.Require(FmCatalog.Level(s)>=10&&s.N("apt_controle")>=3&&Math.Max(s.N("forca"),s.N("destreza"))>=16,"Raio Negro exige nível 10, CL 3 e FOR ou DES 16.");
        var blessed=FmCatalog.Has(s,"faiscas_negras");
        if(blessed)RuleState.Require(FmCatalog.Level(s)>=15&&s.N("apt_controle")>=4&&s.N("apt_aura")>=3,"Faíscas Negras exige nível 15, CL 4 e AU 3.");
        if(natural<(blessed?19:20)-s.Get("kokusen_reducao"))return false;
        if(s.Get("kokusen_desperto")==0)
        {
            s.Set("kokusen_desperto",1);var current=s.Health;var mana=s.Mana;s.Vitals(s.MaxHealth,s.MaxMana+FmCatalog.Level(s));s.Health=current;s.Mana=mana;
            if(s.N("apt_aura")<5)s.Field("apt_aura",s.N("apt_aura")+1);else s.Set("aura_excedente",s.Get("aura_excedente")+1);
        }
        s.Set("kokusen_reducao",Math.Min(Aptitude(s,"controle")/2+(blessed?1:0),s.Get("kokusen_reducao")+1));s.Set("kokusen_duracao",2);
        if(blessed)s.Set("kokusen_bonus",1);
        return true;
    }
    private static int RollDamage(RuleState s,string expression,bool critical)
    {
        if(expression.Length==0) return 0;
        RuleState.Require(DiceExpression.TryParse(expression,out var dice) && dice is not null,"Dano inválido.");
        var total=s.Roll(expression);
        if(critical)
        {
            // Re-roll dice only: flat modifiers and damage-after-attack are never doubled.
            var last=s.Rolls[^1]; total+=s.Roll(expression)-last.Modifier;
        }
        return total;
    }
}
