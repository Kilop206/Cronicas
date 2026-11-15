namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin
{
    private static int Aptitude(RuleState s,string key)
    {
        var value=s.N("apt_"+key);
        return value+(value>0 && key is "aura" or "controle" or "reversa" && s.Get("dominio")>0 && s.Get("confronto")==0?2:0);
    }
    private static string Channel(RuleState s,int points,string? action)
    {
        var advanced=FmCatalog.Has(s,"canalizacao_avancada");var maximum=FmCatalog.Has(s,"canalizacao_maxima");var control=Aptitude(s,"controle");
        if(advanced)RuleState.Require(s.N("apt_controle")>=2&&FmCatalog.Level(s)>=8,"Canalização Avançada exige CL 2 e nível 8.");
        if(maximum)RuleState.Require(advanced&&s.N("apt_controle")>=4&&FmCatalog.Level(s)>=16,"Canalização Máxima exige Avançada, CL 4 e nível 16.");
        RuleState.Require(points>=1&&points<=control+(maximum?1:0),"Quantidade de PE acima do limite de Canalizar em Golpe.");
        RuleState.Require(s.Get("canalizacao_pe")==0,"A canalização anterior ainda está aguardando um acerto.");
        RuleState.Require(action!="reacao"||advanced,"Canalizar como reação exige Canalização Avançada e o gatilho de realizar um ataque confirmado pela mesa.");
        UseAction(s,action=="reacao"?"reacao":"movimento");Spend(s,points);s.Set("canalizacao_pe",points);s.Set("canalizacao_dado",maximum?10:advanced?8:6);
        return $"Canalizados {points} PE: {points}d{s.Get("canalizacao_dado")} durante o próximo ataque que acertar; não se aplica a feitiços.";
    }
    private static string Cover(RuleState s,int points)
    {
        var advanced=FmCatalog.Has(s,"cobertura_avancada");
        if(advanced)RuleState.Require(s.N("apt_controle")>=2&&FmCatalog.Level(s)>=10,"Cobertura Avançada exige CL 2 e nível 10.");
        RuleState.Require(points>=1&&points<=2+2*Aptitude(s,"controle"),"Quantidade de PE acima do limite de Cobrir-se.");
        UseAction(s,"reacao");Spend(s,points);s.Set("cobertura_temp",points*(advanced?8:4));
        return $"Cobrir-se: {s.Get("cobertura_temp")} PV temporários avulsos, sem o teto comum. Confirme o fim do turno da fonte para encerrar.";
    }
    private static string EndCover(RuleState s) {s.Set("cobertura_temp",0);return "Cobrir-se encerrado.";}
    private static string Barrier(RuleState s,int walls)
    {
        RuleState.Require(FmCatalog.Has(s,"tecnicas_barreira")&&s.N("apt_barreira")>=1,"Técnicas de Barreira exige BAR 1.");
        RuleState.Require(walls is >=1 and <=6,"Erga de uma a seis paredes; elas precisam ocupar espaços desocupados dentro de 3 m.");
        var health=BarrierHealth(s);UseAction(s,BarrierAction(s));Spend(s,walls);
        var slots=Enumerable.Range(0,walls).Select(_=>CreateBarrier(s,"parede",health)).ToArray();
        return $"{walls} paredes de 1,5 m erguidas, {health} PV por parede ({string.Join(", ",slots)}). Resolva a posição com o narrador dentro de 3 m, sem sobreposição; registre o dano final em cada parede.";
    }
    private static string SimpleDomain(RuleState s,string action)
    {
        var granted=s.Text("origem")=="sem_tecnica"&&FmCatalog.Level(s)>=4;
        RuleState.Require(FmCatalog.Has(s,"dominio_simples")&&(granted||s.N("apt_barreira")>=1&&FmCatalog.Level(s)>=5),"Domínio Simples exige BAR 1 e nível 5, salvo a concessão do Novo Estilo da Sombra no nível 4.");
        RuleState.Require(s.Get("simples_durabilidade")==0,"Seu Domínio Simples já está ativo.");
        UseAction(s,action);Spend(s,5);StopSustainedConcentration(s);s.Set("concentrando",1);
        s.Set("simples_durabilidade",s.N("apt_barreira")+1);s.Set("simples_raio_meios",1+s.N("apt_dominio"));
        return $"Domínio Simples: {s.Get("simples_raio_meios")*1.5m} m de raio, {s.Get("simples_durabilidade")} de durabilidade. Protege de acerto garantido e efeitos ambientais na sua área."+(action=="reacao"?" Reação à expansão confirmada pela mesa.":"");
    }
    private static string AntiDomainLoss(RuleState s)
    {
        RuleState.Require(s.Get("simples_durabilidade")>0,"Nenhum Domínio Simples ativo.");
        s.Set("simples_durabilidade",s.Get("simples_durabilidade")-1);s.Set("simples_raio_meios",Math.Max(0,s.Get("simples_raio_meios")-1));
        if(s.Get("simples_durabilidade")==0||s.Get("simples_raio_meios")==0)
        {s.Set("simples_durabilidade",0);s.Set("simples_raio_meios",0);s.Set("concentrando",0);return "Domínio Simples quebrou: resolva imediatamente o acerto garantido, se aplicável.";}
        return $"Domínio Simples: {s.Get("simples_durabilidade")} de durabilidade, {s.Get("simples_raio_meios")*1.5m} m de raio.";
    }
    private static void LoseConcentration(RuleState s)
    {
        if(s.Get("simples_durabilidade")>0){AntiDomainLoss(s);return;}
        if(s.Get("cesta_durabilidade")>0){BasketLoss(s);return;}
        s.Set("concentrando",0);StopSustainedConcentration(s);
    }
}
