using Chronicles.Domain;

namespace Chronicles.Application.CharacterSheets;

public sealed class ExecuteCharacterRule(ICharacterRuleStore store, RpgPluginRegistry plugins, IGameRandom random)
{
    public async Task<RuleCommandResponse> Execute(Guid id, Guid actorId, int expectedVersion, RuleCommand? command, CancellationToken ct = default)
    {
        var current = await store.Load(id, actorId, ct);
        if (current is null) return new(Failure: RuleFailure.NotFound);
        if (!CharacterAccessPolicy.CanEdit(current.OwnerId, actorId, current.Role))
            return new(Failure: RuleFailure.Forbidden, Message: "Você não pode alterar este personagem.");
        if (current.State.Version != expectedVersion)
            return new(Failure: RuleFailure.Conflict, Message: "A ficha mudou. Atualize antes de aplicar uma regra.");
        if (command is null) return Invalid("Informe uma ação válida.");
        var master = CharacterAccessPolicy.IsMaster(current.Role);
        if (!master && (command.Visibility == "gm" || command.Action == "configure" && command.Option == "sorte_opcional"))
            return new(Failure: RuleFailure.Forbidden, Message: "Esta ação exige permissão de mestre.");
        if (current.System is null) return Invalid("A campanha não tem sistema associado.");
        var engine = plugins.RulesFor(current.System);
        if (engine is null) return Invalid("Atualize o modelo para usar as ações desta edição.");
        try
        {
            var result = engine.Execute(current.System, current.State, command, random.Draw);
            var errors = CharacterSheetValidation.Validate(result.Character) ??
                CharacterSheetValidation.ValidateFields(current.System, result.Character.Fields, current.State.Fields,
                    master, ruleExecution: true, ruleValidator: engine);
            if (errors is not null) return new(Failure: RuleFailure.Invalid, Errors: errors);
            var saved = await store.Commit(new(current, result, actorId, command.Visibility), ct);
            return saved is null
                ? new(Failure: RuleFailure.Conflict, Message: "A ficha mudou. A ação não foi aplicada; atualize a página.")
                : new(saved, result);
        }
        catch (ArgumentException error) { return Invalid(error.Message); }
    }

    private static RuleCommandResponse Invalid(string message) => new(Failure: RuleFailure.Invalid,
        Errors: new() { ["rules"] = [message] });
}
