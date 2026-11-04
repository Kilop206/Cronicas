namespace Chronicles.Api;

public sealed record RuleResource(string Id, string Label, int Current, int? Maximum, string Recharge, string? Detail = null);

// Base-class resources from the local 2014 PHB. Subclass effects are separate rules.
public static class Dnd2014ClassResources
{
    private static string Used(string key) => "classe_usado_" + key;
    private static int L(RuleState s, string key) => s.Get("nivel_" + key);
    public static RuleResource[] Resources(RuleState s)
    {
        var resources = new List<RuleResource>();
        void Add(string key, string label, int? maximum, string rest, string? detail = null) => resources.Add(new(key, label, maximum is { } cap ? Math.Max(0, cap - s.Get(Used(key))) : 0, maximum, rest, detail));
        var barbarian = L(s, "barbaro"); var bard = L(s, "bardo"); var cleric = L(s, "clerigo"); var druid = L(s, "druida");
        var fighter = L(s, "guerreiro"); var monk = L(s, "monge"); var paladin = L(s, "paladino"); var sorcerer = L(s, "feiticeiro"); var warlock = L(s, "bruxo");
        if (barbarian > 0) Add("furia", "Fúria", barbarian == 20 ? null : barbarian >= 17 ? 6 : barbarian >= 12 ? 5 : barbarian >= 6 ? 4 : barbarian >= 3 ? 3 : 2, "Descanso longo", "Registra usos; efeitos e término da fúria dependem da situação.");
        if (bard > 0) Add("inspiracao", "Inspiração de Bardo", Math.Max(1, s.N("mod_carisma")), bard >= 5 ? "Descanso curto ou longo" : "Descanso longo", "d" + (bard >= 15 ? 12 : bard >= 10 ? 10 : bard >= 5 ? 8 : 6));
        if (cleric >= 2 || paladin >= 3) Add("canalizar", "Canalizar Divindade", cleric >= 18 ? 3 : cleric >= 6 ? 2 : 1, "Descanso curto ou longo", "Clérigo e paladino compartilham os usos na multiclasse.");
        if (druid >= 2) Add("forma_selvagem", "Forma Selvagem", druid == 20 ? null : 2, "Descanso curto ou longo", "Usos da transformação; estatísticas da forma não são substituídas.");
        if (fighter > 0) Add("folego", "Retomar o Fôlego", 1, "Descanso curto ou longo", $"Cura própria: 1d10 + {fighter}.");
        if (fighter >= 2) Add("surto", "Surto de Ação", fighter >= 17 ? 2 : 1, "Descanso curto ou longo", "Uma vez por turno.");
        if (fighter >= 9) Add("indomavel", "Indomável", fighter >= 17 ? 3 : fighter >= 13 ? 2 : 1, "Descanso longo", "Rerrole uma resistência falha e use o novo resultado.");
        if (monk >= 2) Add("ki", "Ki / Chi", monk, "Descanso com 30 min de meditação", $"CD: {8 + s.N("proficiencia") + s.N("mod_sabedoria")}.");
        if (paladin > 0) { Add("cura_maos", "Cura pelas Mãos", paladin * 5, "Descanso longo"); Add("sentido_divino", "Sentido Divino", Math.Max(0, 1 + s.N("mod_carisma")), "Descanso longo"); }
        if (sorcerer >= 2) Add("feiticaria", "Pontos de Feitiçaria", sorcerer, "Descanso longo", sorcerer == 20 ? "Recupera até 4 no descanso curto." : null);
        foreach (var level in new[] { 6, 7, 8, 9 }) if (warlock >= 11 + (level - 6) * 2) Add("arcana_" + level, "Arcana Mística: nível " + level, 1, "Descanso longo", "Uso da magia escolhida, separado dos espaços de pacto.");
        if (warlock == 20) Add("mestre_mistico", "Mestre Místico", 1, "Descanso longo", "Requer 1 minuto suplicando ao patrono.");
        return resources.ToArray();
    }
    public static RuleAction[] Actions(SystemDefinition system, RuleState s)
    {
        var resources = Resources(s);
        var generic = resources.Where(r => r.Id is not ("folego" or "indomavel" or "mestre_mistico")).Select(r => new RuleChoice(r.Id, r.Id=="furia"&&Dnd2014Damage.Supports(system)?r.Label+" (somente registrar gasto)":r.Label)).ToArray();
        var actions = new List<RuleAction>();
        if (generic.Length > 0) actions.Add(new("class-spend", "Gastar recurso de classe", "Quantidade", generic));
        if (L(s, "guerreiro") > 0) actions.Add(new("second-wind", "Retomar o Fôlego e curar a si mesmo"));
        if (L(s, "guerreiro") >= 2) actions.Add(new("class-turn", "Iniciar novo turno (liberar Surto de Ação)"));
        if (L(s, "guerreiro") >= 9) actions.Add(new("indomitable", "Indomável: rerrolar resistência falha", "CD (0: só rolar)", system.Fields.Where(f => f.Key.StartsWith("res_", StringComparison.Ordinal) && f.RollExpression is not null).Select(f => new RuleChoice(f.Key, f.Label)).ToArray()));
        if (L(s, "monge") >= 2) actions.Add(new("short-rest-meditation", "Concluir descanso curto com 30 min de meditação"));
        if (L(s, "paladino") > 0) actions.Add(new("lay-hands-self", "Cura pelas Mãos: curar a si mesmo", "PV"));
        if (L(s, "feiticeiro") >= 2)
        {
            actions.Add(new("sorcery-create", "Criar espaço com Pontos de Feitiçaria", "Nível (1–5)"));
            actions.Add(new("sorcery-convert", "Converter espaço em Pontos de Feitiçaria", "Nível do espaço comum (1–9)", [new("spell", "Espaço de conjuração"), .. (L(s, "bruxo") > 0 ? new RuleChoice[] { new("pact", "Espaço de pacto (nível calculado)") } : [])]));
        }
        if (L(s, "mago") > 0)
        {
            actions.Add(new("arcane-start", "Recuperação Arcana: escolher espaços após descanso curto"));
            actions.Add(new("arcane-slot", "Recuperação Arcana: recuperar um espaço", "Nível (1–5)"));
            actions.Add(new("arcane-end", "Concluir seleção da Recuperação Arcana"));
            actions.Add(new("class-day", "Confirmar novo dia de jogo (Recuperação Arcana)"));
        }
        if (L(s, "bruxo") == 20) actions.Add(new("eldritch-master", "Mestre Místico: concluir 1 minuto e recuperar pacto"));
        return actions.ToArray();
    }
    public static RuleStat[] Stats(RuleState s) => L(s, "mago") == 0 ? [] : [new("Recuperação Arcana no dia", s.Get("arcana_usada") > 0 ? "Já utilizada" : "Disponível após descanso curto"), new("Níveis de espaço ainda a recuperar", s.Get("arcana_saldo").ToString())];
    public static bool Handles(string action) => action is "class-spend" or "class-turn" or "second-wind" or "indomitable" or "lay-hands-self" or "sorcery-create" or "sorcery-convert" or "arcane-start" or "arcane-slot" or "arcane-end" or "class-day" or "eldritch-master";
    public static void BeforeCommand(RuleState s, string action)
    {
        if (action is "arcane-start" or "arcane-slot" or "arcane-end") return;
        s.Set("arcana_descanso", 0); s.Set("arcana_saldo", 0); s.Set("arcana_ativa", 0);
    }
    private static RuleResource Resource(RuleState s, string key) => Resources(s).SingleOrDefault(r => r.Id == key) ?? throw new ArgumentException("Recurso indisponível nos níveis configurados.");
    public static void Spend(RuleState s, string key, int amount)
    {
        var resource = Resource(s, key);
        RuleState.Require(amount > 0 && (resource.Maximum is null || amount <= resource.Current), "Recurso insuficiente ou quantidade inválida.");
        if (key == "surto") { RuleState.Require(amount == 1 && s.Get("surto_turno") == 0, "Surto de Ação só pode ser usado uma vez por turno."); s.Set("surto_turno", 1); }
        if (resource.Maximum is not null) s.Set(Used(key), s.Get(Used(key)) + amount);
    }
    private static int Recover(RuleState s, string key, int amount)
    {
        var resource = Resource(s, key); var maximum = resource.Maximum ?? throw new ArgumentException("A reserva não tem limite de usos.");
        var recovered = Math.Min(amount, maximum - resource.Current);
        s.Set(Used(key), maximum - resource.Current - recovered); return recovered;
    }
    public static void Rest(RuleState s, bool longRest, bool meditation)
    {
        Dnd2014Rage.End(s);s.Set("furia_implacavel_usos",0);s.Set("turno_atividade",0);
        foreach (var resource in Resources(s))
        {
            var shortRecharge = resource.Id is "canalizar" or "forma_selvagem" or "folego" or "surto" || resource.Id == "inspiracao" && L(s, "bardo") >= 5;
            if (resource.Id == "ki") { if (meditation) s.Set(Used(resource.Id), 0); }
            else if (longRest || shortRecharge) s.Set(Used(resource.Id), 0);
        }
        if (!longRest && L(s, "feiticeiro") == 20) Recover(s, "feiticaria", 4);
        s.Set("surto_turno", 0);
        if (!longRest && L(s, "mago") > 0) s.Set("arcana_descanso", 1);
    }
    public static void Initiative(RuleState s)
    {
        if (L(s, "bardo") == 20 && Resource(s, "inspiracao").Current == 0) Recover(s, "inspiracao", 1);
        if (L(s, "monge") == 20 && Resource(s, "ki").Current == 0) Recover(s, "ki", 4);
    }
    public static string Apply(SystemDefinition system, RuleState s, RuleCommand c)
    {
        RuleState.Require(s.Get("morto") == 0, "Personagens mortos não podem usar recursos de classe.");
        if (c.Action is not ("class-day" or "class-turn" or "arcane-end" or "indomitable" or "class-spend")) RuleState.Require(!Dnd2014Conditions.Incapacitated(s), "Incapacitado não pode executar esta ação de classe.");
        switch (c.Action)
        {
            case "class-spend": Spend(s, c.Option!, c.Amount); return "Recurso de classe gasto. Aplique o efeito da habilidade escolhida; o alvo não foi alterado.";
            case "class-turn": s.Set("surto_turno", 0); return "Novo turno confirmado; Surto de Ação liberado para este turno, respeitando os usos restantes.";
            case "second-wind":
                Spend(s, "folego", 1); var heal = s.Roll("1d10+" + L(s, "guerreiro"));
                Dnd2014Rules.Apply(s, new("heal", heal)); return $"Retomar o Fôlego: {s.Rolls[^1].Expression} [{string.Join(", ", s.Rolls[^1].Rolls)}] = {heal}; até {heal} PV recuperados; uso gasto.";
            case "lay-hands-self":
                Spend(s, "cura_maos", c.Amount); Dnd2014Rules.Apply(s, new("heal", c.Amount)); return "Cura própria aplicada e descontada da reserva de Cura pelas Mãos.";
            case "indomitable":
                RuleState.Require(Resource(s, "indomavel").Current > 0, "Sem usos de Indomável.");
                var check = Dnd2014Conditions.Apply(system, s, c with { Action = "test" });
                RuleState.Require(s.Rolls.Count > 0, "Uma falha automática não pode ser melhorada por uma nova rolagem.");
                Spend(s, "indomavel", 1); return "Indomável: use este novo resultado. " + check;
            case "sorcery-create":
                RuleState.Require(c.Amount is >= 1 and <= 5, "Só é possível criar espaços de nível 1 a 5.");
                var cost = new[] { 0, 2, 3, 5, 6, 7 }[c.Amount];
                RuleState.Require(s.Get("slot_criado_" + c.Amount) < 100, "Limite de espaços criados atingido.");
                Spend(s, "feiticaria", cost); var current = Dnd2014Rules.AvailableSlots(s, c.Amount);
                s.Set("slot_usado_" + c.Amount, Dnd2014Rules.TotalSlots(s, c.Amount) - current);
                s.Set("slot_criado_" + c.Amount, s.Get("slot_criado_" + c.Amount) + 1); s.Field("espacos_magia_" + c.Amount, current + 1);
                return $"Espaço de nível {c.Amount} criado por {cost} pontos. Ele desaparece ao concluir descanso longo.";
            case "sorcery-convert":
                RuleState.Require(Resource(s, "feiticaria").Current < Resource(s, "feiticaria").Maximum, "Os Pontos de Feitiçaria já estão completos.");
                var level = c.Option == "pact" ? Math.Min(5, (L(s, "bruxo") + 1) / 2) : c.Amount;
                Dnd2014Rules.Apply(s, c.Option == "pact" ? new("pact-cast") : new("cast", level));
                var recovered = Recover(s, "feiticaria", level);
                return $"Espaço gasto; {recovered} Pontos de Feitiçaria recuperados, respeitando o máximo.";
            case "arcane-start":
                RuleState.Require(L(s, "mago") > 0 && s.Get("arcana_descanso") == 1 && s.Get("arcana_usada") == 0, "Recuperação Arcana exige um descanso curto recém-concluído e só pode ser usada uma vez por dia.");
                s.Set("arcana_usada", 1); s.Set("arcana_ativa", 1); s.Set("arcana_saldo", (L(s, "mago") + 1) / 2); s.Set("arcana_descanso", 0); return "Selecione agora os espaços gastos a recuperar, até o orçamento de níveis disponível.";
            case "arcane-slot":
                RuleState.Require(c.Amount is >= 1 and <= 5 && s.Get("arcana_ativa") == 1 && s.Get("arcana_saldo") >= c.Amount, "Nível inválido, seleção encerrada ou orçamento insuficiente.");
                var available = Dnd2014Rules.AvailableSlots(s, c.Amount); var total = Dnd2014Rules.TotalSlots(s, c.Amount);
                RuleState.Require(available < total, "Nenhum espaço deste nível foi gasto.");
                s.Field("espacos_magia_" + c.Amount, available + 1); s.Set("slot_usado_" + c.Amount, total - available - 1); s.Set("arcana_saldo", s.Get("arcana_saldo") - c.Amount); return "Um espaço recuperado; selecione os restantes ou conclua a recuperação.";
            case "arcane-end": s.Set("arcana_ativa", 0); s.Set("arcana_saldo", 0); s.Set("arcana_descanso", 0); return "Recuperação concluída; o saldo não utilizado foi descartado.";
            case "class-day": s.Set("arcana_usada", 0); return "Novo dia de jogo confirmado. Recuperação Arcana fica disponível no próximo descanso curto.";
            case "eldritch-master": Spend(s, "mestre_mistico", 1); s.Set("pacto_usado", 0); return "Após 1 minuto confirmado, espaços de pacto restaurados; Mestre Místico gasto.";
            default: throw new ArgumentException("Ação de classe desconhecida.");
        }
    }
    public static string? Validate(RuleState s)
    {
        foreach (var pair in s.State.Where(x => x.Key.StartsWith("classe_usado_", StringComparison.Ordinal) || x.Key.StartsWith("slot_criado_", StringComparison.Ordinal)))
            if (pair.Value < 0 || pair.Key.StartsWith("slot_criado_", StringComparison.Ordinal) && pair.Value > 100) return "Saldo de recurso de classe inválido.";
        foreach (var key in new[] { "surto_turno", "arcana_usada", "arcana_descanso", "arcana_ativa" }) if (s.Get(key) is < 0 or > 1) return "Indicador de classe inválido.";
        if (s.Get("arcana_saldo") < 0 || s.Get("arcana_saldo") > (L(s, "mago") + 1) / 2) return "Orçamento de Recuperação Arcana inválido.";
        return null;
    }
}
