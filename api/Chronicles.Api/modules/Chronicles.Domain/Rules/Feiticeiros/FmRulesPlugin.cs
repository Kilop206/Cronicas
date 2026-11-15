using System.Globalization;
using System.Text.Json;

namespace Chronicles.Domain;

public sealed partial class FmRulesPlugin : IRpgRulesPlugin
{
    public string RulesetId => "feiticeiros_maldicoes:2.5.2";
    private static readonly RuleSetting[] Settings = [new("modo_teste", "Testes: −1 desvantagem, 0 normal, 1 vantagem", -1, 1), new("cd_integridade", "CD do efeito de dano na alma", 0, 999), new("sangramento_nivel", "Sangramento: 1 fraco, 2 médio, 3 forte, 4 extremo", 1, 4), new("sangramento_cd", "CD para encerrar sangramento", 0, 999), new("maos_livres", "Mãos livres para expandir domínio", 0, 2), new("pode_falar", "Pode falar (0 não, 1 sim)", 0, 1)];
    private static RuleChoice[] Choices(IEnumerable<string> values) => values.Select(v => new RuleChoice(v, v.Replace('_', ' '))).ToArray();
    public RuleView Describe(SystemDefinition system, CharacterSheetState input)
    {
        EnsureSystem(system); var s = new RuleState(Defaults(system,input)); var levels = FmCatalog.Levels(s); var stage = FmConditions.SoulStage(s);
        var checks = FmCatalog.Skills.Select(c => (Key: "per_" + c.Id, Check: c, Save: false)).Concat(FmCatalog.Saves.Select(c => (Key: "res_" + c.Id, Check: c, Save: true)))
            .ToDictionary(c => c.Key, c => new RuleCheckPreview(c.Check.TrainedOnly && FmConditions.Training(s, c.Check.Id) == 0 ? null : "1d20" + Signed(FmConditions.Bonus(s, c.Check, c.Save)), c.Check.TrainedOnly && FmConditions.Training(s, c.Check.Id) == 0 ? "Exige treinamento." : null));
        var actions = new List<RuleAction> {
            new("fm-initialize", "Aplicar recursos iniciais (uma vez)"), new("fm-recalculate", "Recalcular progressão preservando gastos"),
            new("fm-attribute-start","Iniciar criação de atributos",Options:Choices(["fixos","pontos","rolagem"])),new("fm-attribute-finish","Concluir atributos de criação"),
            new("fm-check", "Teste de perícia / resistência", "CD (0: apenas rolar)", [.. FmCatalog.Skills.Select(c => new RuleChoice("per_" + c.Id, c.Name)), .. FmCatalog.Saves.Select(c => new RuleChoice("res_" + c.Id, c.Name)), new("iniciativa", "Iniciativa")]),
            new("fm-damage", "Receber dano por tipo", "Dano antes das defesas", Choices([.. FmCatalog.DamageTypes, "perda_vida"])),
            new("fm-heal", "Receber cura", "Valor de cura", [new("comum","Comum"),new("reversa","Energia Reversa"),new("alma","Alma · fonte com ER 4 e traçado confirmada")]),
            new("fm-temporary", "Receber recurso temporário (substitui o menor)", "Quantidade", Choices(["vida", "energia"])),
            new("fm-temporary-clear","Encerrar recurso temporário (fim da fonte)",Options:Choices(["vida","energia"])),
            new("fm-spend", "Gastar PE / Estamina", "Custo base"),
            new("fm-short-rest", "Descanso curto (2–4 horas)"), new("fm-hit-die", "Gastar dados de vida no descanso curto", "Quantidade", FmCatalog.Classes.Where(c => levels.ContainsKey(c.Id)).Select(c => new RuleChoice(c.Id,c.Name)).ToArray()),
            new("fm-long-rest", "Descanso longo (8 horas)", Options: Choices(["normal", "fabricacao"])),
            new("fm-condition", "Aplicar condição", "Rodadas (0: até remover)", Choices(FmCatalog.Conditions)),
            new("fm-condition-remove", "Remover condição", Options: Choices(FmCatalog.Conditions)),
            new("fm-turn-start", "Iniciar meu turno"), new("fm-turn-end", "Concluir meu turno"), new("fm-combat-end", "Encerrar cena de combate"), new("fm-new-day", "Confirmar novo dia de jogo"), new("fm-action", "Usar ação", Options: Choices(["comum", "bonus", "movimento", "reacao", "completa", "comum_bonus", "comum_movimento", "bonus_movimento", "levantar"])),
            new("fm-sequence", "Registrar ataques recebidos na sequência", "Quantidade"), new("fm-death-save", "Teste das Portas da Morte"), new("fm-stabilize", "Estabilizar (Medicina / efeito externo confirmado)"),
            new("fm-exhaustion", "Alterar exaustão", "Nível final (0–6)"), new("fm-concentration", "Concentração", "CD (0: iniciar)", Choices(["iniciar", "testar", "encerrar"])),
            new("fm-domain", "Expandir domínio", Options: Choices(["incompleta", "completa", "garantido", "sem_barreiras"])), new("fm-domain-end", "Encerrar domínio"), new("fm-domain-check", "Rolar confronto de domínio (1d10)"),
            new("fm-configure", "Configurar contexto da próxima ação", "Valor", Settings.Select(v => new RuleChoice(v.Key, v.Label)).ToArray())
        };
        var attacks = FmCatalog.Rows(s, "armas");
        if (attacks.Length > 0) actions.Add(new("fm-attack", "Atacar com arma", "Defesa do alvo (0: apenas rolar)", attacks.Select((r,i) => new RuleChoice(i.ToString(CultureInfo.InvariantCulture), FmCatalog.Text(r,"nome"))).ToArray()));
        var spells = FmCatalog.Rows(s, "feiticos");
        if (spells.Length > 0) actions.Add(new("fm-cast", "Conjurar feitiço", "Defesa do alvo (0: apenas rolar)", spells.Select((r,i) => new RuleChoice(i.ToString(CultureInfo.InvariantCulture), FmCatalog.Text(r,"nome"))).ToArray()));
        var sustained=spells.Select((r,i)=>(Row:r,Index:i)).Where(x=>s.Get("sustento_"+x.Index)>0).ToArray();
        if(sustained.Length>0)actions.Add(new("fm-sustain-end","Encerrar sustento",Options:sustained.Select(x=>new RuleChoice(x.Index.ToString(CultureInfo.InvariantCulture),FmCatalog.Text(x.Row,"nome"))).ToArray()));
        if(s.Get("ritual_spell")>0) { actions.Add(new("fm-ritual-finish","Concluir ritual preparado")); actions.Add(new("fm-ritual-cancel","Cancelar ritual preparado")); }
        if(FmCatalog.ClassLevel(s,"lutador")>0)
        {
            actions.Add(new("fm-fighter-defense","Manobra defensiva de Empolgação",Options:Choices(["esquiva","trabalho_pes"])));
            actions.Add(new("fm-maneuver-hit","Confirmar acerto de manobra externa (mesa)"));
        }
        if(FmCatalog.ClassLevel(s,"combate")>0)actions.Add(new("fm-preparation-recover","Recuperar Preparo",Options:Choices(["analisar","eliminacao"])));
        if(FmCatalog.Has(s,"canalizar_golpe")) actions.Add(new("fm-channel","Canalizar em Golpe","PE a canalizar",Choices(FmCatalog.Has(s,"canalizacao_avancada")?["movimento","reacao"]:["movimento"])));
        if(FmCatalog.Has(s,"cobrir_se")) { actions.Add(new("fm-cover","Cobrir-se","PE a gastar")); actions.Add(new("fm-cover-end","Encerrar Cobrir-se (fim do turno da fonte)")); }
        if(FmCatalog.Has(s,"tecnicas_barreira")) actions.Add(new("fm-barrier","Erguer barreiras","Quantidade de paredes (1–6)"));
        if(FmCatalog.Has(s,"dominio_simples")) { actions.Add(new("fm-simple-domain","Ativar Domínio Simples")); actions.Add(new("fm-antidomain-loss","Aplicar desgaste do Domínio Simples")); }
        if (FmCatalog.Has(s, "energia_reversa")) actions.Add(new("fm-reverse", "Autocura com Energia Reversa", "PER (cada PER custa 2 PE)"));
        if (s.Text("origem") == "derivado") actions.Add(new("fm-unnatural", "Energia Antinatural (uma vez por dia)"));
        if (s.Text("origem") == "hibrido") actions.Add(new("fm-hybrid-heal", "Vigor Maldito", "Usos simultâneos"));
        if (levels.GetValueOrDefault("suporte") > 0) actions.Add(new("fm-support-heal", "Suporte em Combate: rolar cura de toque"));
        return new("Feiticeiros & Maldições · 2.5.2", actions.ToArray(), [new("Preparo",$"{Preparation(s)}/{PreparationMaximum(s)}"),new("Empolgação",s.Get("combate")>0&&FmCatalog.ClassLevel(s,"lutador")>0?$"Nível {s.Get("empolgacao",1)} · {ExcitementDice(s)}":"Fora de combate"),new("Bônus de treinamento", Signed(FmCatalog.Training(s))),new("CD da Técnica",FmCatalog.TechniqueDc(s).ToString(CultureInfo.InvariantCulture)),new("Canalização",s.Get("canalizacao_pe")>0?$"{s.Get("canalizacao_pe")}d{s.Get("canalizacao_dado")} no próximo acerto":"Inativa"),new("Domínio Simples",s.Get("simples_durabilidade")>0?$"{s.Get("simples_durabilidade")} de durabilidade · {1.5m*s.Get("simples_raio_meios")} m":"Inativo"),new("Técnica Máxima",s.Get("maxima_recarga")>0?$"Recarga: {s.Get("maxima_recarga")} rodadas":"Disponível se adquirida"), new("Defesa (corpo a corpo / distância)", $"{FmConditions.Defense(s)} / {FmConditions.Defense(s,true)}"), new("Deslocamento", $"{FmConditions.Speed(s)} m"), new("Integridade da alma", $"{Math.Max(0,s.Get("alma_max",s.MaxHealth)-s.Get("alma_dano"))} / {s.Get("alma_max",s.MaxHealth)} · {new[]{"Estável","Danificada","Instável","Crítica"}[stage]}"), new("Portas da Morte", s.Get("morto")>0?"Morto":$"{s.Get("morte_sucessos")} sucessos / {s.Get("morte_falhas")} falhas · déficit {s.Get("vida_negativa")}"), new("Exaustão", s.Get("exaustao").ToString(CultureInfo.InvariantCulture)), new("Condições ativas", string.Join(", ", FmConditions.Active(s))), new("Técnica", s.Get("exaustao_tecnica")>0?$"Exausta por {s.Get("exaustao_tecnica")} rodadas":s.Get("dominio")>0?$"Domínio: {s.Get("dominio_rodadas")} rodadas restantes":"Disponível"), new("Recurso", $"{input.Mana}/{input.MaxMana} {(levels.ContainsKey("restringido")?"Estamina":"PE")} · +{s.Get("energia_temp")} temporários"), new("Turno", s.Get("combate")>0?$"{s.Get("acao_comum")} comum · {s.Get("acao_bonus")} bônus · {s.Get("acao_movimento")} movimento · {s.Get("acao_reacao")} reação":"Fora de combate")], Settings, s.State, checks);
    }
    public RuleResult Execute(SystemDefinition system, CharacterSheetState state, RuleCommand command, Func<int,int> draw)
    {
        if(command.Action=="fm-channel"&&command.Option is null)command=command with{Option="movimento"};
        EnsureSystem(system); var input = Defaults(system,state); var view = Describe(system, input);
        var action = view.Actions.SingleOrDefault(a => a.Id == command.Action) ?? throw new ArgumentException("Ação indisponível nesta edição.");
        RuleState.Require(command.Amount is >= -1 and <= 9999 && command.Visibility is "campaign" or "private" or "gm", "Valor ou visibilidade inválidos.");
        RuleState.Require(action.Options is null || action.Options.Any(o => o.Value == command.Option), "Escolha uma opção válida.");
        var s = new RuleState(input, draw); Validate(s);
        if (command.Action == "fm-configure")
        {
            var setting = Settings.Single(v => v.Key == command.Option);
            RuleState.Require(command.Amount >= setting.Min && command.Amount <= setting.Max, "Valor de contexto fora dos limites.");
            s.Set(setting.Key, command.Amount);
            return new(s.Export(), "Contexto salvo.", []);
        }
        RuleState.Require(command.Amount >= 0, "O valor deve ser positivo.");
        RuleState.Require(s.Get("morto") == 0, "Um personagem morto não pode executar ações.");
        RuleState.Require(command.Action is "fm-initialize" or "fm-check" or "fm-attribute-start" or "fm-attribute-finish" || s.Get("inicializado")==1,"Aplique os recursos iniciais antes de executar ações de combate.");
        var message = command.Action switch
        {
            "fm-initialize" => Initialize(s), "fm-recalculate" => Recalculate(s),
            "fm-attribute-start"=>StartAttributes(s,command.Option!),"fm-attribute-finish"=>FinishAttributes(s),
            "fm-check" => Check(s,command), "fm-damage" => Damage(s,command.Amount,command.Option!),
            "fm-heal" => Heal(s,command.Amount,command.Option!), "fm-temporary" => Temporary(s,command.Amount,command.Option!),
            "fm-temporary-clear"=>ClearTemporary(s,command.Option!),"fm-sustain-end"=>EndSustain(s,command.Option!),
            "fm-spend" => SpendCommand(s,command.Amount), "fm-short-rest" => ShortRest(s), "fm-hit-die" => HitDie(s,command), "fm-long-rest" => LongRest(s,command.Option=="fabricacao"),
            "fm-condition" => Condition(s,command,false), "fm-condition-remove" => Condition(s,command,true),
            "fm-turn-start" => StartTurn(s), "fm-turn-end" => EndTurn(s), "fm-combat-end" => EndCombat(s), "fm-new-day" => NewDay(s), "fm-action" => Action(s,command.Option!),
            "fm-sequence" => Sequence(s,command.Amount), "fm-death-save" => DeathSave(s), "fm-stabilize" => Stabilize(s),
            "fm-exhaustion" => Exhaustion(s,command.Amount), "fm-concentration" => Concentration(s,command),
            "fm-attack" => Attack(s,command), "fm-cast" => Cast(s,command),
            "fm-ritual-finish" => FinishRitual(s,command), "fm-ritual-cancel" => CancelRitual(s),
            "fm-channel" => Channel(s,command.Amount,command.Option), "fm-cover" => Cover(s,command.Amount), "fm-cover-end" => EndCover(s),
            "fm-fighter-defense"=>FighterReaction(s,command.Option!),"fm-maneuver-hit"=>ConfirmManeuverHit(s),"fm-preparation-recover"=>RecoverPreparation(s,command.Option!),
            "fm-barrier" => Barrier(s,command.Amount), "fm-simple-domain" => SimpleDomain(s), "fm-antidomain-loss" => AntiDomainLoss(s),
            "fm-domain" => Domain(s,command.Option!), "fm-domain-end" => EndDomain(s), "fm-domain-check" => DomainCheck(s),
            "fm-reverse" => Reverse(s,command.Amount), "fm-unnatural" => Unnatural(s), "fm-hybrid-heal" => HybridHeal(s,command.Amount), "fm-support-heal" => SupportHeal(s),
            _ => throw new ArgumentException("Ação não implementada.")
        };
        Validate(s); return new(s.Export(),message,s.Rolls.ToArray());
    }
    public string? ValidateStateChange(SystemDefinition system, Dictionary<string,JsonElement> before, Dictionary<string,JsonElement> after, bool master, bool ruleExecution, bool newCharacter)
    {
        try
        {
            EnsureSystem(system);
            var empty = new CharacterInput("Validação",null,null,1,1,0,0,0,0);
            var s = new RuleState(Defaults(system,empty with { Fields=after })); Validate(s);
            if (!ruleExecution)
            {
                var old = new RuleState(empty with { Fields=before });
                if(old.Get("ritual_spell")>0 || old.State.Any(kv=>(kv.Key.StartsWith("sustento_",StringComparison.Ordinal)||kv.Key.StartsWith("concentrado_",StringComparison.Ordinal)) && kv.Value>0))
                    RuleState.Require(before.TryGetValue("feiticos",out var previousSpells) && after.TryGetValue("feiticos",out var nextSpells) && JsonElement.DeepEquals(previousSpells,nextSpells),"Encerre o ritual e os sustentos antes de editar ou reordenar os feitiços.");
                if (newCharacter) RuleState.Require(s.State.Count==0, "Importações novas devem iniciar sem estado de combate; aplique os recursos iniciais.");
                else RuleState.Require(old.State.Count==s.State.Count && old.State.All(kv=>s.State.GetValueOrDefault(kv.Key,int.MinValue)==kv.Value), "Altere estado, recursos de combate e condições pelas ações do sistema.");
            }
            return null;
        }
        catch (ArgumentException e) { return e.Message; }
        catch (Exception e) when (e is InvalidOperationException or JsonException) { return "Estado de Feiticeiros & Maldições malformado."; }
    }
    private static CharacterInput Defaults(SystemDefinition system,CharacterInput input)
    {
        var fields=input.Fields?.ToDictionary(kv=>kv.Key,kv=>kv.Value.Clone()) ?? [];
        RuleState.Require(SheetFormula.Apply(system.Fields,fields) is null,"Fórmulas de Feiticeiros & Maldições inválidas.");
        return input with { Fields=fields };
    }
    private static void EnsureSystem(SystemDefinition system) => RuleState.Require(system.TemplateId==FmCatalog.Id && system.TemplateVersion==1 && system.RulesetVersion==FmCatalog.Edition, "Edição de Feiticeiros & Maldições incompatível.");
    private static string Signed(int n) => n>=0?"+"+n:n.ToString(CultureInfo.InvariantCulture);
    private static void Validate(RuleState s)
    {
        RuleState.Require(FmCatalog.Level(s) is >=1 and <=20, "O nível deve estar entre 1 e 20.");
        RuleState.Require(FmCatalog.Origins.Contains(s.Text("origem")), "Escolha uma origem válida.");
        foreach(var a in FmCatalog.Attributes) RuleState.Require(s.N(a,10) is >=0 and <=30,"Atributo fora dos limites (0–30).");
        var levels = FmCatalog.Levels(s); var restricted=levels.ContainsKey("restringido");
        if(s.Fields.TryGetValue("manobras_lutador",out var maneuvers))
        {
            RuleState.Require(maneuvers.ValueKind==JsonValueKind.Array,"Lista de manobras inválida.");
            var selected=maneuvers.EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.String?v.GetString():null).ToArray();
            var fighter=levels.GetValueOrDefault("lutador");
            RuleState.Require(selected.All(v=>v is not null&&FighterManeuvers.Contains(v))&&selected.Distinct().Count()==selected.Length&&selected.Length<=(fighter>0?2+(fighter>=6?1:0)+(fighter>=12?1:0)+(fighter>=18?1:0):0),"Manobras repetidas, desconhecidas ou acima do limite de Lutador.");
        }
        RuleState.Require(restricted == (s.Text("origem")=="restringido"),"A origem e a especialização Restringido devem ser escolhidas juntas.");
        RuleState.Require(s.Text("origem")!="sem_tecnica" || !levels.ContainsKey("tecnica"),"Sem Técnica não pode ser Especialista em Técnica.");
        RuleState.Require(FmCatalog.Attributes.Contains(s.Text("atributo_tecnica")),"Escolha o atributo da técnica.");
        RuleState.Require(FmCatalog.Main(s).KeyAttributes.Contains(s.Text("atributo_especializacao")),"Atributo incompatível com a especialização inicial.");
        foreach (var key in new[]{"aura","controle","barreira","dominio","reversa"}) RuleState.Require(s.N("apt_"+key) is >=0 and <=5 && (!restricted || s.N("apt_"+key)==0),"Nível de aptidão inválido ou indisponível para Restringido.");
        RuleState.Require(s.Get("exaustao") is >=0 and <=6,"Exaustão inválida.");
        RuleState.Require(s.Get("morte_falhas") is >=0 and <=3 && s.Get("morte_sucessos") is >=0 and <=3 && s.Get("alma_dano")>=0 && s.Get("vida_negativa")>=0,"Estado de vida inválido.");
        foreach(var c in FmCatalog.Conditions) RuleState.Require(s.Get("cond_"+c) is >=-1 and <=100,"Duração de condição inválida.");
        foreach(var (field,checks) in new[]{("pericias",FmCatalog.Skills),("resistencias",FmCatalog.Saves)})
        {
            var seen=new HashSet<string>();
            foreach(var row in FmCatalog.Rows(s,field))
            {
                var key=FmCatalog.Text(row,"chave");
                RuleState.Require(checks.Any(c=>c.Id==key) && seen.Add(key),"Perícia ou resistência inválida/repetida.");
                RuleState.Require(FmCatalog.Number(row,"treino",-1) is >=0 and <=2 && FmCatalog.Number(row,"outros") is >=-99 and <=99,"Treino deve ser 0, 1 ou 2; bônus deve estar entre −99 e 99.");
            }
        }
        foreach(var row in FmCatalog.Rows(s,"defesas")) RuleState.Require((FmCatalog.DamageTypes.Contains(FmCatalog.Text(row,"tipo")) || FmCatalog.Text(row,"tipo") is "todos" or "fisico") && FmCatalog.Text(row,"efeito") is "rd" or "resistencia" or "imunidade" or "vulnerabilidade" && FmCatalog.Number(row,"valor") is >=0 and <=9999,"Defesa por tipo inválida.");
        foreach(var row in FmCatalog.Rows(s,"feiticos"))
        {
            RuleState.Require(FmCatalog.Number(row,"nivel",-1) is >=0 and <=6 && FmCatalog.Number(row,"reducao") is >=0 and <=99,"Nível ou redução de custo do feitiço inválidos.");
            RuleState.Require(FmCatalog.Text(row,"acao") is "comum" or "bonus" or "reacao" or "completa", "Ação do feitiço inválida.");
            RuleState.Require(FmCatalog.Text(row,"tipo") is "ataque" or "resistencia" or "area" or "cura" or "auxiliar" or "passivo", "Tipo do feitiço inválido.");
            RuleState.Require(FmCatalog.Text(row,"duracao") is "imediata" or "duradoura" or "sustentada" or "concentrada", "Duração do feitiço inválida.");
            ValidateDice(FmCatalog.Text(row,"dados"));
        }
        foreach(var row in FmCatalog.Rows(s,"armas"))
        {
            RuleState.Require(FmCatalog.Attributes.Contains(FmCatalog.Text(row,"atributo")) && FmCatalog.Number(row,"margem",20) is >=2 and <=20 && FmCatalog.Number(row,"municao",-1) is >=-1 and <=9999,"Arma inválida.");
            RuleState.Require(FmCatalog.Text(row,"acao") is "comum" or "bonus" or "reacao", "Ação da arma inválida.");
            ValidateDice(FmCatalog.Text(row,"dados")); ValidateDice(FmCatalog.Text(row,"apos"));
        }
    }
    private static void ValidateDice(string expression) => RuleState.Require(expression.Length==0 || DiceExpression.TryParse(expression,out _),"Expressão de dados inválida; use dados estáticos seguros.");
}
