using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronicles.Domain;

public sealed record FrozenSystemTemplate(string Name, string Description, SystemDefinition Definition)
{
    public static FrozenSystemTemplate Capture(string name, string description, SystemDefinition definition)
    {
        // Character rules/presentation need neither the mutable editor history nor private NPC catalogs.
        var json = JsonSerializer.Serialize(definition with { History = null, NpcTemplates = null });
        return new(name, description, SystemDefinition.Read(json));
    }
    public string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)))).ToLowerInvariant();
}
