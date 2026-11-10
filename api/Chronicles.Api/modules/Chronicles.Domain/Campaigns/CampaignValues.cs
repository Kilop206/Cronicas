using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronicles.Domain.Campaigns;

public sealed record CampaignDetails(string Name, string Description, string AccentColor)
{
    public static CampaignDetails? Parse(string? name, string? description, string? color)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed.Length is < 3 or > 120 || description is null || description.Length > 2000 || color?.Length != 7 ||
            !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant) ? null : new(trimmed, description.Trim(), color);
    }
}

public static class CampaignPolicy
{
    public static bool InvitableRole(string? role) => role is "gm" or "co-gm" or "player" or "spectator";
    public static bool ValidStatus(string? status) => status is "planning" or "active" or "paused" or "completed" or "archived";
    public static bool IsOwner(Guid owner, Guid actor, string? role) => actor == owner && role == "owner";
    public static bool MayRemove(Guid owner, Guid actor, Guid target) => target != owner && (actor == owner || actor == target);
    public static bool AvailableInvite(string? role, DateTime expiresAt, DateTime? usedAt, DateTime now) =>
        InvitableRole(role) && usedAt is null && expiresAt > now;
}

public static class CampaignInviteToken
{
    // Preserve the existing 24-byte capability format and case-insensitive hexadecimal links.
    public static bool Valid(string? token) => token?.Length == 48 && token.All(Uri.IsHexDigit);
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()))).ToLowerInvariant();
}
