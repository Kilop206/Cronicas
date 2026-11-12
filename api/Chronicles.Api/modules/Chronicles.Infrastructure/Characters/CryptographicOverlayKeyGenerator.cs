using System.Security.Cryptography;
using Chronicles.Application.Characters;

namespace Chronicles.Infrastructure.Characters;

public sealed class CryptographicOverlayKeyGenerator : IOverlayKeyGenerator
{
    public string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
