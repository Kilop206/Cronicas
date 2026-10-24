using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chronicles.Api.Data;
using Chronicles.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chronicles.Api.Services;

public sealed class SessionService(AppDbContext db, IHostEnvironment environment)
{
    private const string CookieName = "chronicles_session";

    public async Task AuthenticateAsync(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var token) || token.Length != 64) return;
        var hash = TokenHash(token);
        var session = await db.UserSessions.AsNoTracking().Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);
        if (session is null) return;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()), new Claim(ClaimTypes.Name, session.User.DisplayName)], "session"));
        context.Items["sessionId"] = session.Id;
    }

    public async Task StartAsync(HttpContext context, User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddDays(14);
        db.UserSessions.Add(new UserSession { UserId = user.Id, TokenHash = TokenHash(token), ExpiresAt = expires });
        await db.SaveChangesAsync();
        var options = CookieOptions();
        options.Expires = new DateTimeOffset(expires);
        context.Response.Cookies.Append(CookieName, token, options);
    }

    public async Task RevokeAsync(HttpContext context)
    {
        if (context.Items["sessionId"] is Guid id)
        {
            var session = await db.UserSessions.FindAsync(id);
            if (session is not null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        context.Response.Cookies.Delete(CookieName, CookieOptions());
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax, Path = "/", IsEssential = true
    };

    private static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
