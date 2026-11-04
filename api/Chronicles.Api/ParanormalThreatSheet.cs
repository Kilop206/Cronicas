using System.Text.Json;
using System.Text.RegularExpressions;
using static Chronicles.Api.CharacterEndpoints;

namespace Chronicles.Api;

public sealed record ThreatAttributes(int Agilidade = 1, int Forca = 1, int Intelecto = 1, int Presenca = 1, int Vigor = 1);
public sealed record ThreatSkill(string Id, string Name, string Attribute, int Bonus);
public sealed record ThreatAttack(string Id, string Name, string Attribute = "forca", int Bonus = 0, string Style = "melee",
    string Damage = "1d6", string DamageType = "impacto", string ExtraDamage = "0", string ExtraType = "impacto",
    int Count = 1, int Margin = 20, int Multiplier = 2, int Ammunition = -1, int Cost = 0,
    string Action = "standard", string Range = "", string Effect = "");
public sealed record ThreatAbility(string Id, string Name, string Action = "standard", int Cost = 0, string Roll = "",
    int MaximumUses = 0, int RemainingUses = 0, string Recharge = "manual", string Description = "", bool MentalOnly = false);
public sealed record ThreatDefense(string Id, string Type = "all", string Effect = "reduction", int Amount = 0,
    string SourceKind = "base", string SourceId = "base", string Source = "", string[]? Except = null, bool DisabledByEnigma = false);
public sealed record ThreatPresence(bool Enabled = false, int Difficulty = 10, string Damage = "1d6", int? ImmuneNex = null);
public sealed record ThreatFastHealing(int Amount = 0, string[]? Except = null);
public sealed record ThreatSensoryAbilities(bool Scent = false, decimal BlindsightRange = 0, bool LowLightVision = false, bool Darkvision = false);
public sealed record ThreatSensoryContext(bool Active = false, decimal? Distance = null, int Concealment = 0, string Cause = "other", bool OpponentUnseen = false, bool Unavailable = false);
public sealed record ThreatManeuverProfile(bool Configured = false, string Attribute = "forca", int Bonus = 0, bool IncludesSize = true);
public sealed record ThreatManeuverRecord(Guid Id,Guid TargetId,string TargetKind,string TargetName,string Kind,string Outcome,
    int AttackerTotal,int DefenderTotal,int Ties,decimal Displacement,bool ProneApplied,DateTime At);

public sealed record ParanormalThreatSheet
{
    public string TemplateId { get; init; } = "ordem_paranormal";
    public int Version { get; init; } = 1;
    public string RulesetVersion { get; init; } = "1.3";
    public Guid? SystemId { get; init; }
    public string Kind { get; init; } = "paranormal";
    public int Challenge { get; init; }
    public string Element { get; init; } = "none";
    public string Size { get; init; } = "medium";
    public string Descriptors { get; init; } = "";
    public string Senses { get; init; } = "";
    public ThreatSensoryAbilities SensoryAbilities { get; init; } = new();
    public ThreatSensoryContext SensoryContext { get; init; } = new();
    public ThreatManeuverProfile ManeuverProfile { get; init; } = new();
    public bool ManeuverImmune { get; init; }
    public ThreatManeuverRecord[] ManeuverLog { get; init; } = [];
    public string Movement { get; init; } = "";
    public decimal Speed { get; init; }
    public int Defense { get; init; } = 10;
    public bool CriticalImmune { get; init; }
    public int Fortification { get; init; }
    public bool Incorporeal { get; init; }
    public ThreatAttributes Attributes { get; init; } = new();
    public ThreatSkill[] Skills { get; init; } = [];
    public ThreatAttack[] Attacks { get; init; } = [];
    public ThreatAbility[] Abilities { get; init; } = [];
    public ThreatDefense[] Defenses { get; init; } = [];
    public string[] ConditionImmunities { get; init; } = [];
    public string[] ConditionImmunityGroups { get; init; } = [];
    public ThreatPresence Presence { get; init; } = new();
    public Guid[] PresenceTargets { get; init; } = [];
    public ThreatFastHealing FastHealing { get; init; } = new();
    public int Effort { get; init; }
    public int MaxEffort { get; init; }
    public string Enigma { get; init; } = "";
    public bool EnigmaSolved { get; init; }
    public Dictionary<string, int> State { get; init; } = [];
}

public static class ParanormalThreatSchema
{
    public static readonly string[] Attributes = ["agilidade", "forca", "intelecto", "presenca", "vigor"];
    public static readonly string[] Elements = ["sangue", "morte", "conhecimento", "energia", "medo"];
    public static readonly string[] DamageTypes = ["balistico", "corte", "eletricidade", "fogo", "frio", "impacto", "mental", "perfuracao", "quimico", .. Elements];
    public static readonly string[] MentalConditions = ["abalado", "alquebrado", "apavorado", "atordoado", "confuso", "esmorecido", "fascinado", "frustrado", "pasmo"];
    public static readonly Dictionary<string,string[]> ImmunityGroups = new() {
        ["all"] = D20Conditions.Choices(true).Select(c=>c.Value).ToArray(),
        ["mental"] = ["alquebrado","atordoado","confuso","esmorecido","fascinado","frustrado","pasmo"],
        ["fear"] = ["abalado","apavorado"], ["paralysis"] = ["agarrado","enredado","imovel","lento","paralisado"],
        ["fatigue"] = ["exausto","fatigado"], ["senses"] = ["cego","ofuscado","surdo"] };
    public static readonly string[] ActionKinds = ["standard", "move", "full", "reaction", "free", "passive"];
    private static readonly Regex Key = new("^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);
    public static bool CampaignSupports(SystemDefinition? system) => system?.TemplateId == "ordem_paranormal" && system.RulesetVersion == "1.3";
    public static ParanormalThreatSheet? Read(string? json) => json is null ? null : JsonSerializer.Deserialize<ParanormalThreatSheet>(json);
    public static string? Validate(ParanormalThreatSheet sheet)
    {
        try
        {
            void Require(bool valid, string message) => RuleState.Require(valid, message);
            bool Id(string? id) => id is not null && Key.IsMatch(id);
            bool Text(string? value,int max) => value is not null && value.Length <= max;
            Require(sheet.TemplateId == "ordem_paranormal" && sheet.Version == 1 && sheet.RulesetVersion == "1.3", "Versão de ficha de ameaça não suportada.");
            Require(sheet.Kind is "paranormal" or "human" or "animal" && sheet.Size is "tiny" or "small" or "medium" or "large" or "huge" or "colossal", "Tipo ou tamanho de ameaça inválido.");
            Require(sheet.Element == "none" || Elements.Contains(sheet.Element), "Elemento inválido.");
            Require(sheet.Challenge is >= 0 and <= 9999 && sheet.Defense is >= 0 and <= 9999 && sheet.Speed is >= 0 and <= 999, "VD, Defesa ou deslocamento fora dos limites.");
            Require(sheet.Fortification is >=0 and <=100,"Fortificação deve ficar entre 0 e 100% (100% já impede todos os críticos).");
            Require(sheet.SensoryAbilities is not null && sheet.SensoryAbilities.BlindsightRange is >=0 and <=999,"Alcance de Percepção às Cegas deve ficar entre 0 e 999m; zero desativa essa habilidade.");
            Require(ParanormalThreatPerception.ValidateContext(sheet.SensoryContext) is null,ParanormalThreatPerception.ValidateContext(sheet.SensoryContext)??"Contexto sensorial inválido.");
            Require(sheet.ManeuverProfile is not null && Attributes.Contains(sheet.ManeuverProfile.Attribute) && sheet.ManeuverProfile.Bonus is >=-999 and <=9999,"Configure atributo e bônus de manobra válidos.");
            Require(sheet.ManeuverLog is {Length:<=10} && sheet.ManeuverLog.All(r=>r is not null && r.Id!=Guid.Empty && r.TargetId!=Guid.Empty && r.TargetKind is "npc" or "character" && Text(r.TargetName,120) && r.Kind is "trip" or "push" && r.Outcome is "success" or "failed" or "tied" or "blocked" or "concealment" && r.AttackerTotal is >=-9999 and <=20000 && r.DefenderTotal is >=-9999 and <=20000 && r.Ties is >=0 and <=20 && r.Displacement is >=0 and <=12000),"Registro de manobras inválido.");
            Require(Text(sheet.Descriptors,500) && Text(sheet.Senses,2000) && Text(sheet.Movement,500) && Text(sheet.Enigma,5000), "Descrição de ameaça muito longa.");
            Require(sheet.Attributes is not null && new[]{sheet.Attributes.Agilidade,sheet.Attributes.Forca,sheet.Attributes.Intelecto,sheet.Attributes.Presenca,sheet.Attributes.Vigor}.All(n=>n is >= 0 and <= 20), "Atributos devem ficar entre 0 e 20.");
            Require(sheet.Effort >= 0 && sheet.Effort <= sheet.MaxEffort && sheet.MaxEffort is >= 0 and <= 9999, "PE fora dos limites.");
            Require(sheet.State is not null && sheet.State.Count <= 100 && sheet.State.All(pair=>Id(pair.Key) && pair.Value is >= -1 and <= 10000), "Estado de ameaça inválido.");
            Require(sheet.Skills is {Length: <= 40} && sheet.Attacks is {Length: <= 20} && sheet.Abilities is {Length: <= 30} && sheet.Defenses is {Length: <= 40}, "Quantidade de componentes acima do limite.");
            var ids=new HashSet<string>();
            foreach(var skill in sheet.Skills!) Require(skill is not null && Id(skill.Id) && ids.Add(skill.Id) && Text(skill.Name,120) && skill.Name.Length>0 && Attributes.Contains(skill.Attribute) && skill.Bonus is >= -999 and <= 9999, "Perícia de ameaça inválida ou repetida.");
            var state=ToState(sheet,1,1);
            ids.Clear();
            foreach(var attack in sheet.Attacks!)
            {
                Require(attack is not null && Id(attack.Id) && ids.Add(attack.Id) && Text(attack.Name,120) && attack.Name.Length>0 && Attributes.Contains(attack.Attribute), "Ataque inválido ou repetido.");
                Require(attack!.Style is "melee" or "ranged" && attack.Bonus is >= -999 and <= 9999 && attack.Count is >= 1 and <= 10 && attack.Margin is >= 2 and <= 20 && attack.Multiplier is >= 2 and <= 5 && attack.Ammunition is >= -1 and <= 9999 && attack.Cost is >= 0 and <= 9999, "Parâmetros de ataque fora dos limites.");
                Require(ActionKinds.Contains(attack.Action) && attack.Action!="passive" && Text(attack.Range,160) && Text(attack.Effect,2000) && DamageTypes.Contains(attack.DamageType) && DamageTypes.Contains(attack.ExtraType), "Tipo, alcance ou efeito de ataque inválido.");
                Require(attack.Damage is not null && attack.ExtraDamage is not null,"Informe as fórmulas de dano.");
                Require(!sheet.Incorporeal || !UsesStrength(attack.Damage!) && !UsesStrength(attack.ExtraDamage!),"Ameaças incorpóreas não têm Força. Ajuste as referências de dano antes de ativar incorporeidade.");
                DamageFormula.Parse(attack.Damage!,state); DamageFormula.Parse(attack.ExtraDamage!,state);
            }
            ids.Clear();
            foreach(var ability in sheet.Abilities!)
            {
                Require(ability is not null && Id(ability.Id) && ids.Add(ability.Id) && Text(ability.Name,120) && ability.Name.Length>0 && ActionKinds.Contains(ability.Action) && ability.Cost is >= 0 and <= 9999 && ability.MaximumUses is >= 0 and <= 9999 && ability.RemainingUses>=0 && ability.RemainingUses<=ability.MaximumUses && ability.Recharge is "manual" or "turn" or "scene" && Text(ability.Description,4000) && Text(ability.Roll,160),"Habilidade inválida ou repetida.");
                if(ability!.Roll.Length>0) DamageFormula.Parse(ability.Roll,state);
                Require(!sheet.Incorporeal || !UsesStrength(ability.Roll),"Ameaças incorpóreas não têm Força. Ajuste a fórmula da habilidade.");
            }
            ids.Clear();
            foreach(var defense in sheet.Defenses!)
                Require(defense is not null && Id(defense.Id) && ids.Add(defense.Id) && (DamageTypes.Contains(defense.Type)||defense.Type is "all" or "paranormal") && defense.Effect is "reduction" or "immunity" or "vulnerability" && defense.Amount is >=0 and <=9999 && defense.SourceKind is "base" or "ability" or "item" or "ritual" or "ally" && Id(defense.SourceId) && Text(defense.Source,160) && (defense.Except is null || defense.Except.Length<=14 && defense.Except.All(DamageTypes.Contains) && defense.Except.Distinct().Count()==defense.Except.Length),"Defesa de dano inválida ou repetida.");
            Require(sheet.ConditionImmunities is {Length: <= 35} && sheet.ConditionImmunities.All(id=>D20Conditions.Choices(true).Any(c=>c.Value==id)) && sheet.ConditionImmunities.Distinct().Count()==sheet.ConditionImmunities.Length,"Imunidades de condição inválidas.");
            Require(sheet.ConditionImmunityGroups is {Length: <=6} && sheet.ConditionImmunityGroups.All(k=>k is not null && ImmunityGroups.ContainsKey(k)) && sheet.ConditionImmunityGroups.Distinct().Count()==sheet.ConditionImmunityGroups.Length,"Categorias de imunidade inválidas.");
            Require(sheet.Presence is not null && sheet.Presence.Difficulty is >=1 and <=9999 && (sheet.Presence.ImmuneNex is null or 99 || sheet.Presence.ImmuneNex is >=0 and <=95 && sheet.Presence.ImmuneNex%5==0) && sheet.Presence.Damage is not null,"Presença Perturbadora inválida.");
            DamageFormula.Parse(sheet.Presence!.Damage!,state);
            Require(sheet.PresenceTargets is {Length: <=100} && sheet.PresenceTargets.All(id=>id!=Guid.Empty) && sheet.PresenceTargets.Distinct().Count()==sheet.PresenceTargets.Length,"Registro de presença inválido.");
            Require(sheet.FastHealing is not null && sheet.FastHealing.Amount is >=0 and <=9999 && (sheet.FastHealing.Except is null || sheet.FastHealing.Except.Length<=14 && sheet.FastHealing.Except.All(DamageTypes.Contains)),"Cura Acelerada inválida.");
            var indicators=sheet.State!;
            Require(indicators.GetValueOrDefault("cura_impedida") is >=0 and <=9999,"Dano não recuperável por Cura Acelerada inválido.");
            Require(indicators.GetValueOrDefault("dados_zero_penalidade") is >=0 and <=40,"Quantidade de dados para atributo zero inválida.");
            Require(indicators.GetValueOrDefault("chamas_origem") is >=0 and <=5,"Origem das chamas inválida.");
            Require(indicators.GetValueOrDefault("sangrando") is >=0 and <=1 && (indicators.GetValueOrDefault("sangrando")==0||!ImmuneCondition(sheet,"sangrando")),"Sangramento inválido ou impedido por imunidade.");
            foreach(var pair in indicators.Where(p=>p.Key.StartsWith("cond_",StringComparison.Ordinal))) Require(D20Conditions.Choices(true).Any(c=>"cond_"+c.Value==pair.Key) && (pair.Value==0 || !ImmuneCondition(sheet,pair.Key[5..])),"Condição desconhecida ou impedida por imunidade.");
            Require(JsonSerializer.Serialize(sheet).Length<=60000,"A ficha de ameaça excede 60 mil caracteres.");
            return null;
        }
        catch(ArgumentException e){return e.Message;}
    }
    public static RuleState ToState(ParanormalThreatSheet sheet,int health,int maximum,Func<int,int>? draw=null)
    {
        var state=new RuleState(new CharacterInput("Ameaça",null,null,health,maximum,0,0,sheet.Effort,sheet.MaxEffort),draw);
        var a=sheet.Attributes ?? new();
        foreach(var (key,value) in new[]{("agilidade",a.Agilidade),("forca",a.Forca),("intelecto",a.Intelecto),("presenca",a.Presenca),("vigor",a.Vigor),("defesa",sheet.Defense)}) state.Field(key,value);
        state.Fields["deslocamento"]=JsonSerializer.SerializeToElement(sheet.Speed);
        if(sheet.Incorporeal)state.Field("forca",0);
        state.ConditionImmunities=EffectiveImmunities(sheet);
        foreach(var pair in sheet.State ?? []) state.Set(pair.Key,pair.Value);
        foreach(var skill in sheet.Skills ?? []) if(skill is not null) state.Field("per_"+skill.Id,skill.Bonus);
        return state;
    }
    private static bool UsesStrength(string expression) => Regex.IsMatch(expression,@"\{\s*forca\s*\}",RegexOptions.CultureInvariant);
    public static HashSet<string> EffectiveImmunities(ParanormalThreatSheet sheet) => new([
        ..(sheet.ConditionImmunities ?? []),
        ..(sheet.ConditionImmunityGroups ?? []).Where(k=>k is not null && ImmunityGroups.ContainsKey(k)).SelectMany(k=>ImmunityGroups[k]),
        ..sheet.Kind=="paranormal"?MentalConditions:[]],StringComparer.Ordinal);
    public static bool ImmuneCondition(ParanormalThreatSheet sheet,string condition) => EffectiveImmunities(sheet).Contains(condition);
}
