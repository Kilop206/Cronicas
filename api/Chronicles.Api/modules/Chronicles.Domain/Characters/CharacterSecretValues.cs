namespace Chronicles.Domain;

public sealed record CharacterSecretValues(string Notes, int Revision, bool? ExpectedExists = null)
{
    public Dictionary<string, string[]>? Validate() => Notes is null || Notes.Length > 10000
        ? new() { ["notes"] = ["Anotações excedem 10.000 caracteres."] } : null;
}
