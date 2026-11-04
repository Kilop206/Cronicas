using System.Text.Json;

namespace Chronicles.Api;

public sealed record DamagePart(string Type, int Amount, bool Magical = false, bool Silvered = false, bool Adamantine = false);
public sealed record DamageEvent(DamagePart[] Parts, bool Critical = false, bool Attack = false);
public sealed record DamageGroup(string Type, int Raw, int Applied, bool Immune, bool Resistant, bool Vulnerable, string[] Sources);
public sealed record DamageResolution(int Raw, int Total, DamageGroup[] Groups);
public sealed record DndCombatView(RuleChoice[] DamageTypes, bool Raging, int RageTurns, int RageBonus, bool HeavyArmor, int RelentlessDc);

public static class Dnd2014Damage
{
    public const string FieldKey = "defesas_dano";
    public static readonly RuleChoice[] Types = [new("acid","Ácido"),new("bludgeoning","Concussão"),new("cold","Frio"),new("fire","Fogo"),new("force","Energia"),new("lightning","Elétrico"),new("necrotic","Necrótico"),new("piercing","Perfurante"),new("poison","Veneno"),new("psychic","Psíquico"),new("radiant","Radiante"),new("slashing","Cortante"),new("thunder","Trovejante")];
    public static bool Supports(SystemDefinition system) => system.TemplateId == "dnd_5e" && system.TemplateVersion == 5 && system.RulesetVersion == "2014";
    public static bool Physical(string type) => type is "bludgeoning" or "piercing" or "slashing";
    private sealed record Defense(string Type, string Effect, string Condition, string Source);
    private static Defense[] Defenses(RuleState s)
    {
        if (!s.Fields.TryGetValue(FieldKey,out var table)) return [];
        RuleState.Require(table.ValueKind == JsonValueKind.Array && table.GetArrayLength() <= 30,"Defesas de dano inválidas.");
        return table.EnumerateArray().Select(row =>
        {
            string Text(string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key,out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            return new Defense(Text("tipo"),Text("efeito"),Text("condicao"),Text("fonte"));
        }).Where(row => row.Type.Length > 0 || row.Effect.Length > 0 || row.Condition.Length > 0 || row.Source.Length > 0).ToArray();
    }
    public static string? Validate(SystemDefinition system,RuleState s,bool complete = false)
    {
        if (!Supports(system)) return null;
        try
        {
            if(s.N("pontos_temporarios") is <0 or >9999)return "PV temporários devem ficar entre 0 e 9999.";
            if (Defenses(s).Any(row => (row.Type.Length>0 || complete) && row.Type != "all" && !Types.Any(t=>t.Value==row.Type)
                || (row.Effect.Length>0 || complete) && row.Effect is not ("resistance" or "immunity" or "vulnerability")
                || row.Condition is not ("" or "always" or "nonmagical" or "nonmagical_attacks" or "nonmagical_nonsilvered_attacks" or "nonmagical_nonadamantine_attacks"))) return "Escolha tipo, efeito e condição válidos para cada defesa de dano.";
            return Dnd2014Rage.Validate(s);
        }
        catch (ArgumentException e) { return e.Message; }
    }
    private static bool Matches(Defense defense,DamagePart part,bool attack) => (defense.Type == "all" || defense.Type == part.Type) && defense.Condition switch
    {
        "" or "always" => true,
        "nonmagical" => !part.Magical,
        "nonmagical_attacks" => attack && !part.Magical,
        "nonmagical_nonsilvered_attacks" => attack && !part.Magical && !part.Silvered,
        "nonmagical_nonadamantine_attacks" => attack && !part.Magical && !part.Adamantine,
        _ => false
    };
    public static DamageResolution Resolve(SystemDefinition system,RuleState s,DamageEvent? damage)
    {
        RuleState.Require(Supports(system) && system.Fields.Any(f=>f.Key==FieldKey && f.Type=="table"),"Atualize para o modelo D&D 2014 com defesas por tipo.");
        if (Validate(system,s,complete:true) is { } error) throw new ArgumentException(error);
        RuleState.Require(damage?.Parts is {Length: > 0 and <= 12} && damage.Parts.All(part=>part is not null && Types.Any(type=>type.Value==part.Type) && part.Amount is >= 0 and <= 9999),"Informe de 1 a 12 componentes com tipo e dano inteiro válido.");
        RuleState.Require(damage!.Parts.Sum(part=>part.Amount) is >=1 and <=9999,"Informe um dano bruto total entre 1 e 9999.");
        RuleState.Require(!damage.Critical || damage.Attack,"Um crítico exige uma fonte de ataque; os dados extras já devem estar incluídos.");
        var defenses=Defenses(s);
        var components=damage.Parts.Select(part=>
        {
            var matched=defenses.Where(defense=>Matches(defense,part,damage.Attack)).ToArray();
            var petrified=Dnd2014Conditions.Has(s,"petrificado");
            var immune=matched.Any(d=>d.Effect=="immunity") || petrified && part.Type=="poison";
            var resistant=matched.Any(d=>d.Effect=="resistance") || petrified || Dnd2014Rage.Benefits(s) && Physical(part.Type);
            var vulnerable=matched.Any(d=>d.Effect=="vulnerability");
            string[] sources=[..matched.Select(d=>string.IsNullOrWhiteSpace(d.Source)?d.Effect:d.Source),..(petrified?new[]{"Petrificação"}:[]),..(Dnd2014Rage.Benefits(s)&&Physical(part.Type)?new[]{"Fúria"}:[])];
            return new {Part=part,Immune=immune,Resistant=resistant,Vulnerable=vulnerable,Sources=sources};
        });
        // Combine components with the same applicable defenses before rounding; duplicate resistance never stacks.
        var groups=components.GroupBy(p=>new{p.Part.Type,p.Immune,p.Resistant,p.Vulnerable}).Select(group=>
        {
            var raw=group.Sum(p=>p.Part.Amount);var applied=group.Key.Immune?0:group.Key.Resistant?raw/2:raw;
            if(!group.Key.Immune && group.Key.Vulnerable) applied*=2;
            return new DamageGroup(group.Key.Type,raw,applied,group.Key.Immune,group.Key.Resistant,group.Key.Vulnerable,group.SelectMany(p=>p.Sources).Distinct().ToArray());
        }).ToArray();
        return new(damage.Parts.Sum(p=>p.Amount),groups.Sum(g=>g.Applied),groups);
    }
    public static RuleResult Apply(SystemDefinition system,RuleState s,RuleCommand command)
    {
        var result=Resolve(system,s,command.Damage);
        var message=Dnd2014Rules.Apply(s,new("damage",result.Total,command.Damage!.Critical?"critical":"normal"),combat:true);
        var composition=string.Join("; ",result.Groups.Select(g=>$"{Types.Single(t=>t.Value==g.Type).Label}: {g.Raw} → {g.Applied}"));
        return new(s.Export(),$"{composition}. Total aplicado: {result.Total}. {message}",s.Rolls.ToArray(),result);
    }
}
