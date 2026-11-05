using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed record CharacterRulesQueryResult(CharacterRuleSnapshot Character, RPGSystemPlugin? Plugin, RuleView? Rules);

public sealed class ReadCharacterRules(ICharacterRuleStore store, RpgPluginRegistry registry)
{
    public async Task<CharacterRulesQueryResult?> Read(Guid id, Guid actor, CancellationToken ct = default)
    {
        var character = await store.Load(id, actor, ct);
        if (character is null) return null;
        var system = character.System;
        var plugin = system is null ? null : registry.Resolve(system);
        var engine = system is null ? null : registry.RulesFor(system);
        var view = engine?.Describe(system!, character.State);
        if (view is not null) view = view with { Actions = view.Actions.Where(a => !a.Hidden && (a.Options is null || a.Options.Length > 0)).ToArray() };
        return new(character, plugin, view);
    }
}
