using System.Text.Json;

namespace Chronicles.Domain;

// Mechanical values from the locally supplied 2.5.2 rulebook. Prose/artwork is not bundled.
public static class FmCatalog
{
    public const string Id = "feiticeiros_maldicoes";
    public const string Edition = "2.5.2";
    public static readonly string[] Attributes = ["forca", "destreza", "constituicao", "inteligencia", "sabedoria", "presenca"];
    public static readonly FmClass[] Classes = [
        new("lutador", "Lutador", 12, 10, 6, 4, ["forca", "destreza"], 49),
        new("combate", "Especialista em Combate", 12, 10, 6, 4, ["forca", "destreza", "sabedoria"], 63),
        new("tecnica", "Especialista em Técnica", 10, 8, 5, 6, ["inteligencia", "sabedoria"], 78),
        new("controlador", "Controlador", 10, 8, 5, 5, ["presenca", "sabedoria"], 90),
        new("suporte", "Suporte", 10, 8, 5, 5, ["presenca", "sabedoria"], 102),
        new("restringido", "Restringido", 16, 12, 7, 4, Attributes, 114)
    ];
    public static readonly FmCheck[] Skills = [
        new("acrobacia", "Acrobacia", "destreza"), new("atletismo", "Atletismo", "forca"),
        new("direcao", "Direção (complementar)", "sabedoria"), new("enganacao", "Enganação", "presenca"),
        new("feiticaria", "Feitiçaria", "inteligencia", true), new("furtividade", "Furtividade", "destreza"),
        new("historia", "História", "inteligencia"), new("intimidacao", "Intimidação", "presenca"),
        new("intuicao", "Intuição", "sabedoria"), new("investigacao", "Investigação", "inteligencia"),
        new("medicina", "Medicina", "sabedoria", true), new("ocultismo", "Ocultismo", "sabedoria"),
        new("oficio_1", "Ofício I", "inteligencia", true), new("oficio_2", "Ofício II", "inteligencia", true),
        new("percepcao", "Percepção", "sabedoria"), new("performance", "Performance", "presenca"),
        new("persuasao", "Persuasão", "presenca"), new("prestidigitacao", "Prestidigitação", "destreza", true),
        new("sobrevivencia", "Sobrevivência (complementar)", "sabedoria"), new("tecnologia", "Tecnologia", "inteligencia"),
        new("teologia", "Teologia (complementar)", "inteligencia")
    ];
    public static readonly FmCheck[] Saves = [new("astucia", "Astúcia", "inteligencia"), new("fortitude", "Fortitude", "constituicao"), new("integridade", "Integridade", "constituicao"), new("reflexos", "Reflexos", "destreza"), new("vontade", "Vontade", "sabedoria")];
    public static readonly string[] Origins = ["inato", "herdado", "derivado", "restringido", "hibrido", "sem_tecnica", "corpo_mutante"];
    public static readonly string[] DamageTypes = ["cortante", "perfurante", "impacto", "acido", "congelante", "chocante", "queimante", "sonico", "alma", "reversa", "energetico", "psiquico", "radiante", "necrotico", "venenoso"];
    public static readonly string[] Conditions = ["condenado", "engasgando", "enjoado", "envenenado", "sangramento", "sofrendo", "atordoado", "inconsciente", "paralisado", "indefeso", "abalado", "amedrontado", "aterrorizado", "confuso", "enfeiticado", "agarrado", "caido", "enredado", "imovel", "lento", "cego", "desorientado", "desprevenido", "invisivel", "surdo", "surpreso", "exposto", "fragilizado"];
    public static readonly int[] SpellCosts = [0, 2, 5, 8, 12, 20, 25];
    public static readonly string[] TargetDamage = ["1d10", "4d8", "8d8", "14d8", "16d10", "20d12"];
    public static readonly string[] SaveDamage = ["1d10", "3d8", "7d8", "12d8", "14d10", "18d12"];
    public static readonly string[] AreaDamage = ["", "2d8", "4d8", "5d12", "10d10", "12d12"];
    public static int Level(RuleState s) => s.N("nivel", 1);
    public static int Training(RuleState s) => 2 + (Level(s) - 1) / 4;
    public static int Mod(RuleState s, string key) => (int)Math.Floor((s.N(key, 10) - 10) / 2.0);
    public static int ClassLevel(RuleState s,string id) => Classes.Any(c=>c.Id==s.Text("especializacao"))?Levels(s).GetValueOrDefault(id):0;
    public static int EffectLevel(RuleState s,string id) { var real=ClassLevel(s,id); return real==0?0:real+(Level(s)-real)/2; }
    public static int SpellLevel(RuleState s)
    {
        var level=EffectLevel(s,"tecnica");
        return Math.Max(Training(s)-1,ClassLevel(s,"tecnica")>=4?level>=15?5:level>=11?4:level>=7?3:2:0);
    }
    public static int MartialDcBonus(RuleState s) => new[]{"lutador","combate"}.Select(id=>ClassLevel(s,id)>=4?2+(EffectLevel(s,id)>=8?1:0)+(EffectLevel(s,id)>=16?1:0):0).Max();
    public static int TechniqueDc(RuleState s) => 10+Level(s)/2+Training(s)+Mod(s,s.Text("atributo_tecnica"))+s.N("bonus_cd")+MartialDcBonus(s)+(ClassLevel(s,"tecnica")>=10 && s.Text("foco_tecnica")=="refino"?Training(s)/2:0)+(ClassLevel(s,"tecnica")==20?5:0)-s.Get("exaustao");
    public static FmClass Main(RuleState s) => Classes.SingleOrDefault(c => c.Id == s.Text("especializacao")) ?? throw new ArgumentException("Escolha uma especialização válida.");
    public static JsonElement[] Rows(RuleState s, string key) => s.Fields.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];
    public static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    public static int Number(JsonElement row, string key, int fallback = 0) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;
    public static bool Flag(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    public static void Rows(RuleState s, string key, object value) => s.Fields[key] = JsonSerializer.SerializeToElement(value);
    public static bool Has(RuleState s, string ability) => ability=="energia_reversa" && ClassLevel(s,"suporte")>=6
        || ability=="liberacao_reversa" && ClassLevel(s,"suporte")>=8
        || ability=="dominio_simples" && s.Text("origem")=="sem_tecnica" && Level(s)>=4
        || s.Fields.TryGetValue("aptidoes", out var list) && list.ValueKind == JsonValueKind.Array && list.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == ability);
    public static Dictionary<string, int> Levels(RuleState s)
    {
        var levels = new Dictionary<string, int> { [Main(s).Id] = Level(s) };
        var rows = Rows(s, "multiclasse");
        if (rows.Length == 0) return levels;
        levels.Clear();
        foreach (var row in rows)
        {
            var key = Text(row, "classe"); var level = Number(row, "nivel");
            RuleState.Require(Classes.Any(c => c.Id == key) && level is >= 1 and <= 20 && levels.TryAdd(key, level), "Multiclasse inválida ou repetida.");
        }
        RuleState.Require(levels.Values.Sum() == Level(s) && levels.ContainsKey(Main(s).Id), "A soma da multiclasse deve corresponder ao nível e incluir a especialização inicial.");
        RuleState.Require(!levels.ContainsKey("restringido") || levels.Count == 1, "Restringidos não podem fazer multiclasse.");
        RuleState.Require(s.Text("origem") != "corpo_mutante" || levels.Count == 1, "Núcleos de corpos mutantes não podem fazer multiclasse.");
        foreach (var key in levels.Keys.Where(k => k != Main(s).Id))
        {
            var attributes = key is "lutador" or "combate" ? new[] { "forca", "destreza" } : key == "tecnica" ? ["inteligencia", "sabedoria"] : new[] { "presenca", "sabedoria" };
            RuleState.Require(attributes.Any(a => s.N(a) >= 16), "A multiclasse exige um atributo elegível com valor 16.");
        }
        return levels;
    }
}

public sealed record FmClass(string Id, string Name, int InitialHealth, int HitDie, int HealthGain, int EnergyGain, string[] KeyAttributes, int Page);
public sealed record FmCheck(string Id, string Name, string Attribute, bool TrainedOnly = false);
