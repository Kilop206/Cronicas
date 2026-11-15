namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static (int Health,int Energy) BaseResources(RuleState s)
    {
        var main=FmCatalog.Main(s); var levels=FmCatalog.Levels(s); var level=FmCatalog.Level(s);
        var health=main.InitialHealth-main.HealthGain + levels.Sum(kv=>FmCatalog.Classes.Single(c=>c.Id==kv.Key).HealthGain*kv.Value) + FmCatalog.Mod(s,"constituicao")*level+s.N("bonus_pv");
        var energy=levels.Sum(kv=>FmCatalog.Classes.Single(c=>c.Id==kv.Key).EnergyGain*kv.Value)+s.N("bonus_pe");
        if(main.Id is "tecnica" or "controlador" or "suporte") energy+=FmCatalog.Mod(s,s.Text("atributo_tecnica"));
        if(FmCatalog.ClassLevel(s,"tecnica")>=10 && s.Text("foco_tecnica")=="economia") energy+=FmCatalog.Training(s);
        if(s.Get("kokusen_desperto")>0)energy+=FmCatalog.Level(s);
        if(s.Text("origem")=="herdado"&&s.Text("heranca_cla")=="gojo")energy+=level/2;
        if(s.Text("origem")=="herdado"&&s.Text("heranca_cla")=="kamo")health+=level-1+(level>=10?FmCatalog.Mod(s,"constituicao"):0);
        RuleState.Require(health is >=1 and <=9999 && energy is >=0 and <=9999,"Totais de recursos fora dos limites; confira atributos e bônus.");
        return(health,energy);
    }
    private static string Initialize(RuleState s)
    {
        RuleState.Require(s.Get("inicializado")==0,"Os recursos iniciais já foram aplicados. Use recalcular progressão para preservar os gastos.");
        RuleState.Require(s.Get("criacao_metodo")==0||s.Get("criacao_concluida")==1,"Conclua a distribuição guiada dos atributos antes de aplicar recursos.");
        var (health,energy)=BaseResources(s); s.Vitals(health,energy,0); s.Set("inicializado",1); s.Set("pv_base",health); s.Set("alma_max",health);
        s.Set("pode_falar",1); s.Set("maos_livres",2); s.Set("sangramento_nivel",1);
        return "PV, integridade da alma e PE/Estamina iniciais aplicados. Confira as escolhas de origem, talentos e habilidades na ficha.";
    }
    private static string Recalculate(RuleState s)
    {
        RuleState.Require(s.Get("inicializado")==1,"Aplique os recursos iniciais primeiro.");
        var (health,energy)=BaseResources(s); var deficitHealth=s.MaxHealth-s.Health; var deficitEnergy=s.MaxMana-s.Mana;
        var oldSoul=s.Get("alma_max",s.MaxHealth); var soulLost=s.Get("alma_dano");
        s.Set("pv_base",health); s.Set("alma_max",health); s.Set("alma_dano",Math.Min(health,soulLost));
        var maximum=EffectiveHealthMaximum(s); s.Vitals(Math.Max(1,maximum),energy); s.Health=Math.Max(0,maximum-deficitHealth); s.Mana=Math.Max(0,energy-deficitEnergy);
        if(maximum==0 || health<=soulLost) s.Set("morto",1);
        if(oldSoul!=health) s.Set("alma_max",health);
        s.Set("vida_temp",Math.Min(s.Get("vida_temp"),s.MaxHealth/2)); s.Set("energia_temp",Math.Min(s.Get("energia_temp"),s.MaxMana/2));
        return "Progressão recalculada com ganhos fixos de PV e modificador retroativo de Constituição. Gastos e dano na alma preservados; bônus de escolhas ficam nos campos próprios.";
    }
    private static int EffectiveHealthMaximum(RuleState s)
    {
        var basic=s.Get("pv_base",s.MaxHealth+s.Get("dano_alma")); var exhaustion=s.Get("exaustao");
        var penalty=exhaustion>=5?Math.Max(50,basic/2):exhaustion>=3?Math.Max(20,basic/4):0;
        // Exhaustion that would eliminate HP causes a coma rather than death (p.324).
        var reduced=basic-penalty;
        s.Set("desmaiado",reduced<=0?1:0);
        return Math.Max(0,Math.Max(1,reduced)-s.Get("dano_alma"));
    }
    private static string Damage(RuleState s,int raw,string type,FmActionContext? context=null)
    {
        RuleState.Require(raw>0,"Informe dano maior que zero."); s.Set("descanso_curto",0);
        context??=new();RuleState.Require(context.IgnoreReduction is >=0 and <=9999,"RD ignorada fora dos limites.");
        RuleState.Require(type is not ("alma" or "perda_vida")||context.IgnoreReduction==0&&!context.IgnoreResistance,"Dano na alma/perda de vida já ignora defesas; não declare efeitos adicionais de Penetrante/Raio Negro.");
        var amount=raw;
        if(type=="alma")
        {
            var degree=FmConditions.Save(s,"integridade",s.Get("cd_integridade"));
            amount=degree==2?0:degree==1?raw/2:raw;
            s.Set("dano_alma",Math.Min(9999,s.Get("dano_alma")+amount)); s.Set("alma_dano",Math.Min(s.Get("alma_max",s.MaxHealth),s.Get("alma_dano")+amount));
            var oldHealth=s.Health; var maximum=EffectiveHealthMaximum(s); s.ChangeHealthMaximum(maximum); s.Health=Math.Max(0,oldHealth-amount);
            if(maximum==0 || s.Get("alma_dano")>=s.Get("alma_max",s.MaxHealth)) s.Set("morto",1);
            if(oldHealth<=amount && s.Get("morto")==0) EnterDying(s,Math.Max(0,amount-oldHealth),oldHealth==0);
        }
        else
        {
            if(type!="perda_vida") amount=DefendedDamage(s,raw,type,context);
            ApplyHealthLoss(s,amount,type!="perda_vida");
        }
        if(amount>0 && s.Get("concentrando")!=0)
        {
            if(FmConditions.Save(s,"fortitude",Math.Max(10,amount/2),true)==0) LoseConcentration(s);
        }
        if(amount>=50 && amount*2>=s.MaxHealth) s.Set("ferimento_pendente",1);
        return $"{raw} de {type}: {amount} aplicado(s)."+(s.Get("ferimento_pendente")>0?" Ferimento Complexo: defina o membro/efeito com o narrador (p.314).":"")+(s.Get("morto")>0?" Personagem morto.":"");
    }
    private static int DefendedDamage(RuleState s,int amount,string type,FmActionContext context)
    {
        var rows=FmCatalog.Rows(s,"defesas").Where(r=>FmCatalog.Text(r,"tipo")==type || FmCatalog.Text(r,"tipo")=="todos" || FmCatalog.Text(r,"tipo")=="fisico" && type is "cortante" or "perfurante" or "impacto").ToArray();
        if(rows.Any(r=>FmCatalog.Text(r,"efeito")=="imunidade") || type=="reversa" && s.Text("natureza")!="maldicao" || type=="venenoso"&&s.Text("origem")=="corpo_mutante") return 0;
        var fragile=FmConditions.Active(s).Contains("fragilizado");
        var resistance=!context.IgnoreResistance && !fragile && (rows.Any(r=>FmCatalog.Text(r,"efeito")=="resistencia") || FmCatalog.ClassLevel(s,"restringido")==20 && type is "cortante" or "perfurante" or "impacto");
        var vulnerable=rows.Any(r=>FmCatalog.Text(r,"efeito")=="vulnerabilidade") || type=="reversa" && s.Text("natureza")=="maldicao";
        // Multiplication/division precede flat RD (appendix p.365).
        var adjusted=(int)Math.Floor(amount*(resistance?0.5m:1m)*(vulnerable?1.5m:1m));
        var reduction=fragile?0:rows.Where(r=>FmCatalog.Text(r,"efeito")=="rd").GroupBy(r=>FmCatalog.Text(r,"fonte")).Sum(g=>g.Max(r=>FmCatalog.Number(r,"valor")));
        if(!fragile && FmCatalog.ClassLevel(s,"lutador")>=2) reduction+=FmCatalog.EffectLevel(s,"lutador")/2;
        return Math.Max(0,adjusted-Math.Max(0,reduction-context.IgnoreReduction));
    }
    private static void ApplyHealthLoss(RuleState s,int amount,bool absorbTemporary)
    {
        if(amount<=0) return;
        if(absorbTemporary) { var absorbed=Math.Min(amount,s.Get("cobertura_temp")); amount-=absorbed; s.Set("cobertura_temp",s.Get("cobertura_temp")-absorbed); }
        if(absorbTemporary) { var absorbed=Math.Min(amount,s.Get("vida_temp")); amount-=absorbed; s.Set("vida_temp",s.Get("vida_temp")-absorbed); }
        if(amount==0) return;
        var before=s.Health; s.Health-=amount;
        if(s.Get("morrendo")>0) { s.Set("vida_negativa",Math.Min(9999,s.Get("vida_negativa")+amount)); AddDeathFailure(s); }
        else if(amount>=before) EnterDying(s,amount-before,before==0);
        if(s.Get("vida_negativa")>s.MaxHealth) s.Set("morto",1);
        if(FmConditions.Incapacitated(s)) { s.Set("concentrando",0); StopSustainedConcentration(s); if(s.Get("dominio")>0) EndDomain(s); }
    }
    private static void EnterDying(RuleState s,int deficit,bool extraFailure)
    {
        s.Set("morrendo",1); s.Set("vida_negativa",deficit); s.Set("morte_sucessos",0);
        if(s.Get("exaustao")>=4) s.Set("morte_falhas",Math.Max(2,s.Get("morte_falhas")));
        if(extraFailure) AddDeathFailure(s);
        if(deficit>s.MaxHealth) s.Set("morto",1);
    }
    private static void AddDeathFailure(RuleState s,int amount=1) { s.Set("morte_falhas",Math.Min(3,s.Get("morte_falhas")+amount)); if(s.Get("morte_falhas")>=3) s.Set("morto",1); }
    private static string Heal(RuleState s,int amount,string type)
    {
        RuleState.Require(amount>0,"Informe cura maior que zero.");
        if(type=="alma")
        {
            // Receiving healing does not require the recipient to have the healer's aptitude.
            // This option explicitly confirms an external source with ER4 and soul-outline knowledge.
            var restored=Math.Min(s.Get("dano_alma"),amount/2); var health=s.Health;
            s.Set("dano_alma",s.Get("dano_alma")-restored); s.Set("alma_dano",Math.Max(0,s.Get("alma_dano")-amount/2));
            s.ChangeHealthMaximum(EffectiveHealthMaximum(s)); s.Health=health; amount=restored;
        }
        if(type=="reversa") { RuleState.Require(s.Text("natureza")!="maldicao","Maldições não recebem cura de energia reversa."); if(s.Text("origem")=="hibrido") amount/=2; }
        HealPoints(s,amount); return $"Cura aplicada: {amount}.";
    }
    private static void HealPoints(RuleState s,int amount)
    {
        if(s.Get("morrendo")>0)
        {
            var deficit=s.Get("vida_negativa"); s.Set("vida_negativa",Math.Max(0,deficit-amount));
            if(amount>=deficit) { Stabilize(s); s.Health=Math.Max(1,amount-deficit); }
        }
        else s.Health+=amount;
    }
    private static string Stabilize(RuleState s)
    {
        RuleState.Require(s.Get("morrendo")>0,"O personagem não está nas Portas da Morte.");
        s.Set("morrendo",0); s.Set("vida_negativa",0); s.Set("morte_sucessos",0); s.Health=1;
        return "Estabilizado com 1 PV; as falhas de morte persistem até descanso longo.";
    }
    private static string DeathSave(RuleState s)
    {
        RuleState.Require(s.Get("morrendo")>0,"O personagem não está nas Portas da Morte.");
        var die=s.Die(20);
        if(die<10) AddDeathFailure(s,die==1?2:1); else s.Set("morte_sucessos",Math.Min(3,s.Get("morte_sucessos")+(die==20?2:1)));
        if(s.Get("morte_sucessos")>=3) return Stabilize(s);
        return s.Get("morto")>0?"Três falhas: personagem morto.":$"Portas da Morte: {s.Get("morte_sucessos")} sucessos, {s.Get("morte_falhas")} falhas.";
    }
    private static string Temporary(RuleState s,int amount,string option)
    {
        var key=option=="vida"?"vida_temp":"energia_temp"; var maximum=option=="vida"?s.MaxHealth:s.MaxMana;
        s.Set(key,Math.Max(s.Get(key),Math.Min(amount,maximum/2))); return "Temporários atualizados: mantém o maior valor, limitado à metade do máximo.";
    }
    private static string ClearTemporary(RuleState s,string option){s.Set(option=="vida"?"vida_temp":"energia_temp",0);return "Recurso temporário encerrado com sua fonte.";}
    private static string EndSustain(RuleState s,string option){s.Set("sustento_"+option,0);return "Sustento encerrado; não será cobrado no próximo turno.";}
    private static void Spend(RuleState s,int basic,bool penalty=true)
    {
        RuleState.Require(basic>=0,"Custo inválido."); var amount=basic+(penalty?FmConditions.CostPenalty(s):0);
        RuleState.Require(s.Mana+s.Get("energia_temp")>=amount,"PE/Estamina insuficientes.");
        var temporary=Math.Min(amount,s.Get("energia_temp")); s.Set("energia_temp",s.Get("energia_temp")-temporary); s.Mana-=amount-temporary;
    }
    private static string SpendCommand(RuleState s,int amount) { RuleState.Require(amount>0,"Informe o custo base."); Spend(s,amount); return "Custo pago, incluindo penalidades de alma e Condenado."; }
    private static string ShortRest(RuleState s)
    {
        RuleState.Require(s.Get("morrendo")==0 && s.Get("combate")==0,"Encerre o combate e estabilize antes do descanso.");
        s.Mana+=s.MaxMana/2; s.Set("descanso_curto",1); s.Set("suporte_usos",0);s.Set("preparo_gasto",Math.Max(0,s.Get("preparo_gasto")-PreparationMaximum(s)/2)); return "Descanso curto concluído: metade do recurso máximo e do Preparo recuperada. Agora pode gastar dados de vida.";
    }
    private static string HitDie(RuleState s,RuleCommand c)
    {
        RuleState.Require(s.Get("descanso_curto")==1,"Confirme um descanso curto antes de gastar dados de vida.");
        var definition=FmCatalog.Classes.Single(x=>x.Id==c.Option); var available=FmCatalog.Levels(s).GetValueOrDefault(definition.Id)-s.Get("dv_"+definition.Id);
        RuleState.Require(c.Amount>0 && c.Amount<=available,"Dados de vida insuficientes.");
        var heal=Enumerable.Range(0,c.Amount).Sum(_=>Math.Max(0,s.Die(definition.HitDie)+FmCatalog.Mod(s,"constituicao")-FmConditions.OutputPenalty(s)));
        s.Set("dv_"+definition.Id,s.Get("dv_"+definition.Id)+c.Amount); HealPoints(s,heal); return $"{c.Amount}d{definition.HitDie} gastos: {heal} PV recuperados.";
    }
    private static string LongRest(RuleState s,bool crafting)
    {
        RuleState.Require(s.Get("morrendo")==0 && s.Get("combate")==0,"Encerre o combate e estabilize antes do descanso.");
        s.Set("dano_alma",s.Get("dano_alma")-s.Get("dano_alma")/2); s.Set("exaustao",Math.Max(0,s.Get("exaustao")-1));
        s.ChangeHealthMaximum(EffectiveHealthMaximum(s));
        if(crafting) { s.Health+=s.MaxHealth/2; s.Mana+=s.MaxMana/2; }
        else { s.Health=s.MaxHealth; s.Mana=s.MaxMana; }
        foreach(var cls in FmCatalog.Classes) s.Set("dv_"+cls.Id,crafting?Math.Max(0,s.Get("dv_"+cls.Id)-FmCatalog.Levels(s).GetValueOrDefault(cls.Id)/2):0);
        foreach(var key in new[]{"morte_falhas","morte_sucessos","vigor_usos","suporte_usos","energia_temp","vida_temp","exaustao_tecnica","descanso_curto","preparo_gasto"}) s.Set(key,0);
        // Once-per-day abilities deliberately use an explicit new-day command, not elapsed real time.
        return "Descanso longo concluído: recursos e dados recuperados, falhas de morte removidas e exaustão reduzida em 1. Metade da perda de PV máximos por dano na alma restaurada; integridade não é presumida como curada.";
    }
}
