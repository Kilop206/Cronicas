namespace Chronicles.Domain;

public sealed record InventoryItemValues(string Name, string Category, int Quantity, decimal Weight, string Notes)
{
    public const int Capacity = 200;
    public Dictionary<string, string[]>? Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Name?.Trim().Length is not (>= 1 and <= 120)) errors["name"] = ["Informe um nome com até 120 caracteres."];
        if (Category is not ("item" or "equipment" or "weapon" or "consumable" or "other")) errors["category"] = ["Categoria inválida."];
        if (Quantity is < 1 or > 9999) errors["quantity"] = ["Quantidade entre 1 e 9999."];
        if (Weight is < 0 or > 9999.99m || decimal.Round(Weight, 2) != Weight) errors["weight"] = ["Peso entre 0 e 9999,99 com até duas casas decimais."];
        if (Notes is null || Notes.Length > 1000) errors["notes"] = ["Notas com até 1000 caracteres."];
        return errors.Count == 0 ? null : errors;
    }

    public InventoryItemValues Normalize() => this with { Name = Name.Trim(), Notes = Notes.Trim() };
}

public sealed record CharacterAbilityValues(string Name, string Description, string Cost, string Effect, string Duration,
    string Limitations, string Recharge, string? RollExpression)
{
    public const int Capacity = 100;
    public Dictionary<string, string[]>? Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Name?.Trim().Length is not (>= 1 and <= 120)) errors["name"] = ["Informe um nome com até 120 caracteres."];
        if (Description is null || Description.Length > 4000 || Effect is null || Effect.Length > 4000 ||
            Cost is null || Cost.Length > 80 || Duration is null || Duration.Length > 80 ||
            Limitations is null || Limitations.Length > 1000 || Recharge is null || Recharge.Length > 80)
            errors["ability"] = ["Um ou mais campos excedem o limite."];
        if (!string.IsNullOrWhiteSpace(RollExpression) && (RollExpression.Length > 40 || !DiceExpression.TryParse(RollExpression, out _)))
            errors["rollExpression"] = ["Use uma expressão como 1d20+5 ou 2d6."];
        return errors.Count == 0 ? null : errors;
    }

    public CharacterAbilityValues Normalize() => new(Name.Trim(), Description.Trim(), Cost.Trim(), Effect.Trim(), Duration.Trim(),
        Limitations.Trim(), Recharge.Trim(), string.IsNullOrWhiteSpace(RollExpression) ? null : RollExpression.Trim().ToLowerInvariant());
}
