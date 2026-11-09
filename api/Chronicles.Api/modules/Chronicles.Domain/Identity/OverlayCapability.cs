using System.Security.Cryptography;
using System.Text;

namespace Chronicles.Domain;

public static class OverlayCapability
{
    public static bool IsValid(string? key) => key is { Length: 64 } && key.All(Uri.IsHexDigit);
    public static string Hash(string key)
    {
        if (!IsValid(key)) throw new ArgumentException("Invalid overlay capability.", nameof(key));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()))).ToLowerInvariant();
    }
}
