using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Chronicles.Api;

// The socket peer is Render/Cloudflare in production. Only our signed edge headers identify a visitor.
public sealed class ClientRequestIdentity(string? proxyKey)
{
    public string ClientIp(HttpContext context, long? nowSeconds = null)
    {
        var headers = context.Request.Headers;
        var ip = headers["X-Cronicas-Client-IP"];
        var timestamp = headers["X-Cronicas-Proxy-Time"];
        var signature = headers["X-Cronicas-Proxy-Signature"];
        var now = nowSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (proxyKey is { Length: >= 32 } && ip.Count == 1 && timestamp.Count == 1 && signature.Count == 1
            && ip.ToString().Length <= 64 && IPAddress.TryParse(ip.ToString(), out var address)
            && timestamp.ToString().Length <= 20 && long.TryParse(timestamp.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var sent)
            && sent >= now - 90 && sent <= now + 90 && signature.ToString().Length == 64)
        {
            try
            {
                var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(proxyKey), Encoding.UTF8.GetBytes($"v1\n{timestamp}\n{ip}"));
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature.ToString()))) return Normalize(address);
            }
            catch (FormatException) { /* Invalid signatures never change the trusted identity. */ }
        }
        return context.Connection.RemoteIpAddress is { } peer ? Normalize(peer) : "unknown";
    }

    public string Actor(HttpContext context) => context.User.Identity?.IsAuthenticated == true
        && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? "user:" + id.ToString("N") : "ip:" + ClientIp(context);

    private static string Normalize(IPAddress address) => (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}

public static class RequestRateLimits
{
    public static IServiceCollection AddRequestRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var identity = new ClientRequestIdentity(configuration["PROXY_IDENTITY_KEY"]);
        services.AddSingleton(identity);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => Window("auth:" + context.Request.Path.ToString().TrimEnd('/').ToLowerInvariant() + ":ip:" + identity.ClientIp(context), 20, TimeSpan.FromMinutes(5)));
            options.AddPolicy("account", context => Window("account:" + identity.Actor(context), 60, TimeSpan.FromMinutes(1)));
            options.AddPolicy("rolls", context => Window("rolls:" + identity.Actor(context), 120, TimeSpan.FromMinutes(1)));
            options.OnRejected = async (rejected, token) =>
            {
                var wait = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var remaining) ? Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)) : 60;
                rejected.HttpContext.Response.Headers.RetryAfter = wait.ToString(CultureInfo.InvariantCulture);
                rejected.HttpContext.Response.Headers.CacheControl = "no-store";
                await rejected.HttpContext.Response.WriteAsJsonAsync(new { message = $"Limite temporário de requisições atingido. Tente novamente em {wait} segundos.", retryAfterSeconds = wait }, token);
            };
        });
    }

    private static RateLimitPartition<string> Window(string key, int permits, TimeSpan window) => RateLimitPartition.GetFixedWindowLimiter(key,
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });
}
